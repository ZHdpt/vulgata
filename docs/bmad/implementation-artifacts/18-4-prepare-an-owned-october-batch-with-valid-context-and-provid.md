# Story 18.4 development record

Status: in-progress (paused at recovery7/7 after recurring T029 fixture-order assertion; accepted definition retained)
Feature directory: `specs/017-october-batch-setup`
Workflow state: `specs/017-october-batch-setup/workflow-state.json`

## Current delivery boundary (2026-10-02)

The human clarified ordinary Web startup with only its database configuration
changed and instructed delivery to proceed on that basis. The special selected-data
Web startup described below is historical, not the current target: Web must require
no October mode, batch ID, setup-operator credentials or generator provider binding.
Independent batch setup with Web stopped retains exact selection, authorization,
provider/context provenance, source18.1 and persistence-isolation obligations.
Root reconciled the affected October requirement/proposal wording against the exact
decision in `evidence/ordinary-web-user-decision-01.json`. The retained Astra-medium
author corrected only affected specification/plan/tasks. Independent plan-review-15
and definition-review-14 accepted the revised definition; scoped delegate decisions
plan-authorization-06 and implementation-approval-08 are bound and validated.
Only T018/T025/T026 were reopened: 23 of the same30 IDs remain completed, with seven
open tasks. Counts remain6/7; old diagnostic failures are not
relabeled as passing and no old-path runtime retry is implied. Historical approvals
below remain evidence for unchanged semantic scope, not revised Web implementation
approval. New delivery uses the retained separate test/production owners and all
remaining verification gates. Ordinary Web now captures final DefaultConnection at
Build for Identity/domain/factory contexts and has no special October startup,
batch/operator authentication or generator provider loading. The retained test owner
verified honest RED/minimal GREEN and controlled final runs of64/64 and43/43;
the latter includes the six legacy-setting cases, setup-service absence and normal
browser/role/system-provider consumers. SQLite oracle/model-warning fixture errors
and launcher input errors remain separately recorded, not product RED or counted
workflow failures. See `evidence/ordinary-web-controlled-final-green-01.json`.
No source18.1 or generator files changed. Obsolete selected-Web-only tests were
removed with their Git/raw history preserved. Real PostgreSQL migrations, ordinary
Web A/B processes and the E2E provider-administration class remain unqualified;
runtime consumer reconciliation, residual owned-type cleanup and final gates are
next. No task completion markers changed during this bounded controlled checkpoint.
The user then instructed: "Pause at next safe checkpoint". Root completed only the
already-running T026 cleanup and its43/43 controlled check, reconciled/compiled the
two runtime-test files, and saved owned checkpoint20. The unused selected-data
binder/bound-setup files and startup selection declarations are removed; certificate
types needed by tracked legacy consumers and the protected activation draft remain.
Deleted source/test files remain recoverable in Git. No new PostgreSQL/E2E resources
or app processes were launched. On explicit resume, retain the same test owner to
fill existing positive separate-generator-Identity and actual-PG/process provider
administration coverage gaps under T020/T029, then qualify and finish all gates.
Recovery6/7,23/30 task markers, current owners/approvals and historical evidence are
unchanged. The user pause is not a workflow failure, waiver or completion.

## Scoped test slimming (2026-10-05)

The human requested: "Slim the tests as much as possible. Leave only necessary
tests to close the story as fast as possible". This authorizes bounded test-only
consolidation, not a waiver of accepted behavior or review/verification gates.
Normal delivery, PostgreSQL/E2E resource acquisition and production changes remain
paused. The unavailable retained test session is replaced in the same Luna/high
test-owner role from current raw definition, accepted reviews and historical test
evidence. Only October tests and necessary suite inventory edits are assigned;
unrelated dirty files, definition/production artifacts and task markers are protected.
The implement dispatch guard passed against checkpoint20 before this assignment.
Recovery remains6/7 and approvals/definition ownership remain unchanged. Extra
Build-time capture is still the current accepted plan: this request does not itself
authorize deleting its production code or dropping its coverage. Consolidation must
preserve source authority, context/provider provenance, exact selection, cancellation,
ordinary Web and persistence isolation; real-runtime qualification remains pending.

The test owner combined the six legacy-setting cases and two route-only startup
cases into one ordinary-host test: eight Web starts become one. All six legacy
keys, login availability, anonymous login redirect and all four retired route
absence assertions remain. The existing route helper moved to the formerly
route-only source file, whose suite inventory entry remains valid; no empty stub
or deletion workaround remains. The accepted final-Build, setup-service absence,
generator/current-authority/source/revocation/cancellation/context/provider and
database isolation tests are unchanged. Further generator diagnostic cuts were
rejected because the executable parser/credential/authority entrypoints protect
distinct accepted behavior rather than duplicate controlled coverage.
One current-source focused/surrounding SelfContained launcher run passed37/37,
failed0/skipped0, execution11seconds, with policy/guard checks and successful build:
`TestResults/isolation-6c691a6b3879456483b5f2d8b494c7b0`. TRX SHA256
`5a03c4c12d37453392c1de2f4845ba7d1f54bf9d140fbbabfd98ce0178b899c9`;
build-log SHA256 `68cd660f02c181d569c536efb1b4f998a7fdc57c34c746775a84ff99cf1aedc7`.
Final assessment: coverage-preserving test consolidation; no production refactor,
fabricated RED, scope change or qualification claim. This is not final verification
or story closure. Normal delivery/runtime remain user-paused after checkpoint21;
all required independent gates and pending T020/T029 qualification remain.

## Resumed qualification (2026-10-05)

Actual human instruction: "Postgre sql is on. Proceed". This lifts the earlier
delivery pause, retaining the slimmed tests and every existing gate/resource limit.
Root verified HEAD `2d438c6a11812dfeff2a662566c5a3345f383b00`, assigned branch
`oct14/story-18-4`, checkpoint21 binding, accepted spec/plan/tasks hashes and ancestor
merge/source18.1 baselines. Docker reports29.8.2. Readiness is not permission to use
an ambient database: existing recorded authorization covers fresh owned PG17
containers/volumes/databases, synthetic fixtures and recorded generator/Web processes.
Only remaining accepted T020/T029 coverage and qualification are dispatched initially;
positive separate generator Identity and real ordinary-host provider administration
are existing gaps, not new requirements. No Web redesign or source18.1 contract
change is inferred from this resume. Recovery6/7, definition profile, approvals and
23/30 task markers remain intact; unrelated dirty files remain protected.

First fresh PostgreSQL qualification passed95/96, no skips. The new provider-admin
login helper incorrectly expected relative `Location: /`, whereas normal Identity
returned the same-origin absolute root redirect. Primary and cleanup diagnostics
are separate; all owned databases/temp roots, logger locks, container and volume
cleanup passed. This first development test-oracle defect is noncounting under the
actual human accounting correction, not a production RED or a qualification pass.
See `evidence/ordinary-web-pg-provider-login-oracle-01.json`. The retained owner is
assigned one corrected affected-case/class rerun; existing95 passes are retained
for their unchanged frozen scope. Actual-process provider CRUD belongs in the
existing A/B test, not an additional host suite. No production change is authorized
by this oracle diagnosis; recovery remains6/7 and all final gates remain pending.

The URI correction passed. The next check exposed role count3 versus1, and the
attempted synthetic-account deletion did not fix it. Root independently verified
that `CaptureOrdinaryDataStateAsync` counts roles, not users: A already ran ordinary
`RoleSeeder`, whereas B had not yet started Web and had only its fixture SystemOwner
role. The earlier worker/root user-count hypothesis was incorrect. Normal seeding
is required and correct; do not suppress it or weaken the comparison. Move the
existing B ordinary startup before equivalent ordinary-shape snapshots, assert
all normal seeded roles after each host starts and preserve every October/history/
cross-target check. This needs no extra host or production/definition change.

Run `9355c7a248c94a2d908ecb40d488d1a0` first exposed this distinct fixture-order defect;
run `36cdfff6758f49cebee545d70524ae2f` reproduced it after attempted account cleanup.
Only the latter is counted as one recurring failed repair under the actual human
accounting correction. Existing historical6 becomes7; initial development failures
and duplicate reports remain noncounting. Raw TRX, frozen test hashes and diagnosis
are preserved in `evidence/ordinary-web-pg-role-order-recurring-failure-01.json` and
`evidence/ordinary-web-pg-role-order-diagnosis-01.json`. Transition123 records the
single increment;124 records the current at-count7 diagnosis. Owner is tests, not
spec/plan/tasks, so accepted plan15/definition14 and approvals06/08 remain valid.
The existing delegate ceiling is7; only the human may grant a fresh finite7-to8
extension for the bounded fixture-order correction and affected rerun. All workers
are idle. Every run verified exact owned database/temp/container/volume cleanup;
actual E2E/process provider-admin qualification has not started. Source/production
and23/30 task markers remain unchanged. Preserve the two unverified test edits
unstaged; checkpoint22 contains root evidence/state only, never failing test code.
No further repair, runtime invocation, new resources or definition restart proceeds
through this pause. This is allowance only: all remaining qualification/convergence/
lookback/independent quality/E2E/final gates still apply and failure8 would pause again.

## Historical pre-correction boundary (2026-10-01)

The later user authorization supersedes the historical plan-only restrictions below.
Plan-review-13 and definition-review-12 accepted the preceding definition; delegated
bindings 04/06 remain historical authority for unchanged scope, not advance approval
of corrected artifacts. The retained Astra-medium owner completed the bounded T015
caller/session-lifetime and provider-seam plan correction; deterministic validation
passed and independent plan-review-14 accepted it at transition 0077. Delegated
plan-authorization-05 permits affected task reconciliation only. All 30 task IDs and
completion markers are retained; independent definition-review-13 accepted the
corrected definition. Separate delegated implementation-approval-07 is bound, with
count6/limit7 unchanged. The target remains an
independent batch executable with Web stopped and a whole Web dataset selected once
from startup configuration, changed only by restart. No online authority transport
or runtime dataset switch belongs to this accepted definition.

Direct Identity authentication, current management selection and database-backed
source approval issue/verify/revoke have controlled passing evidence. The actual
18.1 issuer and three closed borrowed Worker functions passed 20 focused and 344
surrounding tests through the approved outside-sandbox SelfContained launcher after
restricted native root-open denial. Context closure, per-Repository isolation and
cancellation settlement are tested; 18.1 source contracts are unchanged. Console
entry has controlled no-echo tests and a separate static native-adapter inspection,
not native-terminal runtime qualification.

Actual source-current-check construction exists only in trusted integration tests,
not in a production caller. Earlier T015 required such a caller/context lifetime while the
accepted setup-only generator contract exits and disposes its session without source
work. The current candidate reconciles that gap as production callable adapters
qualified together by the controlled caller, not an executable source consumer;
contexts settle and dispose before the caller's session. It preserves direct
authority, exact root mapping, existing issuer/functions and all controlled evidence.
No later-story source analysis or external prerequisite is created. Independent
plan/definition acceptance and bounded T015 task reconciliation are now recorded;
production completion and runtime qualification are not yet claimed. The first
discovery is a noncounting invalidation, with count6/limit7 intact.

The generator module's direct orchestration passed independent final coverage (8/8)
and surrounding regressions (344/344). The first actual selected-Web startup tracer
now passes 9/9, with 234/234 current surrounding checks: direct authentication and
current authorization bind the exact existing batch before serving. This is
in-process WAF/owned SQLite evidence. The initial options replacement masked production
connection declarations; the corrected fixture captures Application/Vulgata Npgsql
callbacks before translating exact A/B declarations to separately owned SQLite targets.
October uses the production authority's declared data connection. Final startup/route
checks pass 17/17 and mapped surrounding checks pass 249/249. A previously absolute
Identity lockout expiry elapsed during this run; the fixture now establishes and
asserts a currently locked account, preserving Denied/no-payload checks and leaving
production authorization unchanged. See current composition final-GREEN evidence.
The concrete provider binder is now independently verified: 36/36 focused and
270/270 mapped surrounding tests pass without skips. Changed atomic binding,
invalid-input, credential-exception and cancellation behaviors retain separate
expected RED/minimal GREEN evidence. Shared/unused credentials, defensive copies
and real ephemeral Data Protection compatibility have truthful test-only coverage.
Both retained owners assessed the affected units; no refactor was needed. This is
binder verification, not provider availability or generation qualification.
Actual selected-Web single-document loading now passes independent 12/12 focused
and 249/249 surrounding tests. It validates/binds before authentication, rejects
ambiguous/malformed documents atomically, accepts identical selector values and
does not rebind after document mutation/configuration reload. Changed behavior
retains expected RED/minimal GREEN; already-correct loading/freeze behavior uses
passing coverage. Both retained owners assessed final affected units without a
required refactor. Generator startup invocation now has actual executable coverage;
Identity descriptor enforcement and Web binding retention are now verified under T018/T019/T024.
No plaintext is returned, no paid provider is contacted, and recovery remains
count6/limit7. See `evidence/provider-binding-final-green-01.json` and
`evidence/selected-web-provider-final-green-01.json`. Executable/PG qualification
and remaining tasks are not implied by this controlled WAF/SQLite checkpoint.

T021/T022 context projection is complete through already implemented storage and
selection: 1/1 combined member/standalone projection case and 166/166 surrounding
tests pass. Stored scope/origin/applicability/null/empty/advisory differences,
digest and first-commit time survive fresh same-authority advisory edits; foreign
colliding authority denial reuses existing selector coverage. No new production
store, writer or manufactured RED was required. See
`evidence/context-projection-final-green-01.json` and retained T022 assessment.

Actual generator prepare/provider composition now passes independent model5/5,
real child1/1 with Web stopped and surrounding270/270, without skips. Fresh authorized
PostgreSQL17 resources and exact cleanup are verified. Public EF diagnostics exposed
incomplete frozen snapshot/target metadata; the bounded correction leaves migration
Up/Down and runtime context unchanged. The earlier static completeness assumption is
disproved by retained raw diagnostics, not rewritten. Both owners assessed final units;
no further refactor is required. See `evidence/generator-prepare-final-green-01.json`.
Generator prepare/select/parser rejection and invalid credentials now pass9/9 actual
process cases under fresh owned PostgreSQL17, with exact database/temp/container/volume
cleanup. Controlled Web split Identity, cookie-free401 versus permitted-cookie200,
ordinary operational service absence and actual missing-batch Denied diagnostics pass
22/22 and262/262 surrounding checks. The login helper now selects the named form;
raw fixture comparison/prebuild-policy failures remain history, not product RED or
workflow-count increments. No production change was needed for this coverage.
See `evidence/generator-loader-auth-final-green-01.json` and
`evidence/selected-web-denial-final-green-01.json`.
FR010 actual-CLI diagnostics now retain expected RED/minimal GREEN and independent
final verification: 19/19 executable cases and 272/272 mapped surrounding tests pass.
Main reports Prepared/Selected and exact batch identity, provider configuration Bound,
and sanitized closed failure classes. Phase-bounded startup validation includes the
demonstrated non-exact identity ArgumentException without weakening identity checks
or relabeling later authority/storage failures. Both affected units were assessed;
no further refactor was needed. Exact owned database/temp/container/volume cleanup
is verified. See `evidence/generator-cli-diagnostics-final-green-01.json`.
Cancellation remains qualified at existing callable seams, not as actual CLI
interruption: no new cancellation protocol is invented. Accepted Identity descriptor
enforcement now guards all five fresh-authority entrypoints, including issue/verify/revoke,
and successful Web provider binding is retained for the selected process lifetime.
Authorized test-only fixture consolidation preserves all assertions and owned-resource
isolation. Fresh final checks pass93/93 PostgreSQL authority/source,20/20 actual generator,
3/3 selected-Web Identity WAF and187/187 October SelfContained; exact containers/volumes
are absent. Both affected units are assessed with no further refactor needed. See
`evidence/identity-group-final-green-01.json`. Selected-Web WAF/SQLite data translation
does not prove full PostgreSQL/listener behavior. Full A/B qualification and final gates
remain unfinished. Actual-Web qualification exposed the management serializable transaction
outside its configured Npgsql retry strategy; Web exits Unavailable before serving.
T019 is reopened for the bounded private correction with observed actual-process RED in
`evidence/selected-web-management-retry-transaction-red-01.json`. The complete serializable
capture now runs inside the configured strategy with fresh retry-local state and preserved
cancellation/disposal. Actual A/B minimal GREEN1/1 and exact cleanup are independently
verified in `evidence/selected-web-management-retry-minimal-green-01.json`; final surrounding
qualification is still pending. An independent URI assertion correction preserves same-origin
login redirection and anonymous authority behavior. The original transaction defect is not
reproduced; no failed repair or independent gate verdict is counted for these development checks.
The same30IDs now have26 completed markers; T019/T020/T029/T030 remain open.
The expanded actual host-selection lane passed21/21; this is not complete T020 coverage
or a final group check. The PostgreSQL WAF lane is blocked after three bounded log-lifetime
checks. Derived and parent disposal do not release the process-owned log; cleanup masks
an earlier setup/assertion exception, so no Web assertion PASS can be inferred. All exact
owned containers, volumes and roots are absent. Preserve the primary exception separately
before any additional finite diagnostic execution. Raw proof and unchanged recovery6/7
are bound in `evidence/postgresql-fixture-log-lifetime-blocked-01.json` and transition0111.
Checkpoint17 preserves this diagnostic recovery boundary, not a verified delivery candidate.
Recovery remains6/7 with unchanged approvals. The first broad regression also exposed 18 obsolete
approval-API host cases; their accepted T018/T025 reconciliation now passes 17 focused
and 249 surrounding cases. The failed broad run is retained rather than described as
passing. Current tests prove route absence and reject obsolete inputs, not real
database or native-host qualification.
Current evidence is in `workflow-state.json` and the local authority/source issuer
and independent generator module evidence under the existing feature directory.
The real headless-authority/module/store prepare tracer now verifies the persisted
snapshot through the actual selector. The lockout fixture uses a future real-Identity
expiry and asserts the locked-out precondition, preserving Revoked/null expectations.
Final focused coverage passes 2/2 and surrounding authority/source/Web coverage passes
267/267, with no skips. Earlier 84/85 and collection-assertion failures remain raw
history, not expected RED or workflow-count increments. Revised T015's production
adapters conform without edits/refactoring and current regression completes that
task; whole host and PostgreSQL qualification remain open.

Expected behavioral RED and ordinary development build/test corrections do not
consume recovery allowance. The existing episode remains 6/7 with its diagnosis,
holds, retained Astra-medium definition ownership and model strategy preserved.
Unavailable implementation/test sessions were restored under the same Sol-medium
and Luna-high responsibilities from raw artifacts, without restarting definition.
Actual subsequent user permission explicitly grants fresh runner-owned PostgreSQL17
A/B (optional separate Identity), synthetic fixtures/keys/source roots and owned
generator/Web processes on isolated loopback ports, with exact owned cleanup. See
runtime-qualification-resource-authorization-01.json. This grants resources only,
not approval of revised definition artifacts or a recovery extension; the separate
definition approvals and remaining verification gates are not waived. No shared database/credentials,
paid provider or unrelated process is authorized.
Earlier grant-based plans/results and scope restrictions below are historical,
not the current implementation authority.

## Lifecycle story

### Story 18.4: Prepare an Owned October Batch with Valid Context and Provider Bindings

As a batch operator,
I want one valid isolated batch setup shared by the generator and October Web,
So that generation and later readers address the intended data without polluting normal or other batch data.

**Requirements:** O-FR01, O-FR02, O-FR17, O-FR18, O-FR19; O-AR01-O-AR04, O-NFR03.
**Completion dependencies:** 18.1.
**Parallel start:** source identity and wiki/setup F0; no JDT/CodeGraph or model analysis dependency.

**Acceptance Criteria:**

**Given** an explicitly authorized owned database target and real operator identity,
**When** programmatic October setup is requested,
**Then** valid System/Repository/context bindings and October batch metadata are created through shared rules,
**And** only this story's setup records/mappings are introduced; no fake administrator, dummy pair/FlowGroup, forced ScanRun or new CRUD UI is used.

**Given** two distinct owned batch databases with identical fixed schema names,
**When** generator and October Web start with an exact database and batch ID,
**Then** both bind the selected setup and reject missing/mismatched selection without previous/latest-batch fallback,
**And** a switch requires restart; no read/write crosses into the other target.

**Given** selected Global/System/Repository context and explicit provider configuration,
**When** setup is bound,
**Then** actual context values/origins and conflicts are retained, and API type/Worker/Chat role profiles and configuration precedence are explicit,
**And** no unspecified `.env` discovery, borrowed credentials, silent field mixing or automatic failover occurs; protected-row copying requires compatibility only if that route is used.

**Given** October enabled versus ordinary startup,
**When** mappings/migrations and registration are evaluated,
**Then** the October persistence context, migration history and model snapshot are isolated; ordinary startup and the normal model remain unchanged,
**And** setup emits sanitized diagnostics without claiming tool readiness, paid-provider readiness or completed Knowledge generation.

## Current authorized delivery boundary

- The user lifted the historical plan-only/merge-only restrictions and authorized normal governed delivery in this assigned branch/worktree. Accepted corrected plan-review-13 and definition-review-12 preserve independent batch execution and startup-only Web dataset selection; delegated decisions 04/06 bind the corrected artifacts. Story 18.1's integrated contracts remain unchanged and require this story's actual use-path verification.
- The bounded console seam clarification changes only testability, not authentication policy or resource scope. Existing source/authentication/protected-file evidence remains valid for its tested slices; controlled interactive input and current authority have passing evidence, while native-terminal and actual host/runtime qualification remain distinct and incomplete.
- The user corrected root's mistaken first-build failure count. Expected RED and ordinary first-pass development failures are noncounting; independent negative reviews/convergence and recurrence after repair remain countable. Raw erroneous accounting is retained as superseded evidence; the existing historical episode remains at 6/7 with its owners, diagnosis, holds and extension preserved. No recovery extension or waiver was requested for this correction.
- Actual PostgreSQL/migration/process/credential/key/terminal/provider qualification still needs explicit authorized owned resources. No merge, push, history rewrite, later-story work or other checkout access is authorized. All remaining reviews, convergence and final verification gates remain mandatory.

## Historical planning-only authorization (2026-09-28)

- The user authorized Story 18.4 definition and planning only. Task generation, implementation, production/test changes, runtime/provider use and migration execution are prohibited in this run.
- Independently decidable planning covers owned batch setup, exact database and batch selection, context origin/conflict handling, explicit provider-role binding/configuration precedence, and isolation from ordinary startup and other batch databases.
- Story 18.1 is actively implemented in a different checkout. This worktree must not inspect or modify that checkout, duplicate its source-access contracts, invent source identity/tool contracts, or claim the dependency is satisfied.
- The specification and plan must identify every seam that consumes Story 18.1's accepted source identity/tool contracts. Those seams remain unresolved dependencies until an integrated, verified 18.1 baseline is present in this worktree and the plan is reconciled against its actual accepted contracts.
- Delivery must stop after an independently reviewed plan. No planned-change authorization is requested merely to document the dependency, and no tasks may be generated. Before any later continuation, root must verify the integrated 18.1 baseline, reconcile specification/plan assumptions and hashes, repeat affected review, and satisfy any then-applicable planned-change authorization.

## Activation and ownership

- Observed source commit: `5e5553374b342fb658776fbfcb86c6e06513832a`.
- Assigned checkout: `C:/Users/13728/.codex/worktrees/d166/vulgata`.
- The app/user supplied this separate managed worktree as a native handoff. It is detached at the supplied commit; no branch mutation was performed.
- Initial index/worktree was clean. The root owns lifecycle state, evidence, exact-path staging and checkpoints. Definition workers may write only the current feature's stage-scoped definition artifacts and may not stage or commit.
- Lifecycle advanced from backlog through accepted story readiness to in-progress for Story 18.4 only; Epic 18 is in-progress. Story 18.1's tracker entry and separate implementation remain untouched.

## Dependency hold

Definition and plan authoring may proceed using only accepted repository authorities and the explicit dependency boundary above. Task generation and every later delivery stage are held pending an integrated, verified Story 18.1 baseline and plan reconciliation against its actual accepted source identity/tool contracts.

## Specification result

- The persistent definition author completed `specs/017-october-batch-setup/spec.md` and its requirements checklist. Root verified the returned hashes and the checklist's complete result.
- The specification makes owned setup, exact database/batch selection, context provenance/conflicts, provider-role binding and October/ordinary isolation independently plannable.
- FR-011, SC-006 and the Prepared Source Binding entity keep Repository/root/source identity, source grant and contained read-only tool binding explicitly dependent on Story 18.1. They do not define or claim those contracts.
- Specification self-review: `specs/017-october-batch-setup/evidence/spec-self-review-01.json`.
- No application/test/runtime/provider/database work occurred. The optional agent-context hook was not run because the stage write boundary excluded it.

## Architecture plan result

- The ordinary definition author classified the plan as `architecture` before writing because the accepted story requires a dedicated October persistence boundary and a shared generator–Web exact-selection contract. Plan authorship therefore moved to the required retained `plan-architect-high` profile; the ordinary author remains retained for later non-architecture phases.
- The plan selects one explicitly owned PostgreSQL database per batch, a fixed `october` schema and separate EF context/migration history/model snapshot, one shared setup/selection policy, exact immutable process-lifetime database/batch selection, retained context origins/conflicts, and complete explicitly sourced Worker/Chat provider profiles.
- Ordinary Web Identity/domain composition and migrations remain separate and unchanged. No new package, generator executable, CRUD UI, provider/database execution, source/test/migration edit or task is introduced by planning.
- Plan IDs `ID-I04`, `ID-L03` and `ID-T04` reserve the prepared-source adapter, reuse and verification seams for the actual integrated 18.1 contracts. They deliberately leave contract names, storage shape, cardinality and grant semantics unspecified.
- Deterministic plan-content validation passed the architecture front matter, required headings and stable ID shapes. All specification requirements and success criteria map to design IDs or the explicit 18.1 hold.
- Plan self-review: `specs/017-october-batch-setup/evidence/plan-self-review-01.json`.
- The architecture classification requires independent plan-only review. Current plan content does not itself establish an AGENTS.md Worker-tool, Scan-strategy or PRD-change authorization trigger; root and reviewer must reassess after 18.1 reconciliation.
- The optional provider-source preference remains nonblocking: the draft uses one explicitly supplied, source-neutral configuration bundle per process, with no unspecified `.env` discovery or protected-row copy.

## Independent plan review and correction

- Plan review 01 returned `REVISE` with one HIGH plan-owned finding: the first draft named an authenticated operator and owned target but did not define non-forgeable authority sources for October Web and the future headless generator. The recovery episode retains one failed result; no production started.
- The retained architecture plan owner corrected the seam. Existing Web Identity and live `ManagementAccess` issue a short-lived, purpose-bound activation grant; October Web verifies it locally and the generator validates it through Web. Deployment-owned allowlists map opaque target IDs to canonical database descriptors, both hosts verify connected database identity before October migration/setup, and protected use repeats live authorization. Standalone Repository setup requires current management authority and a real `SystemId == null` Repository. This setup grant remains separate from Story 18.1 source grants.
- Plan review 02 independently returned `ACCEPT` with no findings and confirmed the architecture classification, design IDs, corrected trust boundary, testability, absence of a current Worker-tool/Scan-strategy/PRD planned-change trigger, and continued 18.1 hold.
- The raw accepted plan verdict is preserved at `specs/017-october-batch-setup/evidence/plan-review-02.json`. The complete-definition recovery episode cannot close until tasks and the later full definition review exist. Because the user prohibited task generation in this run, workflow state remains before tasks and no planned-change authorization or implementation approval is requested.

## Ready boundary and required resume

- Ready now: reviewed specification and architecture plan for owned setup, exact database/batch selection, context provenance/conflicts, explicit provider profiles, separate October persistence/migrations, ordinary-startup isolation, non-forgeable activation authority, and controlled/PostgreSQL verification seams.
- Still dependent on 18.1: `FR-011`, `SC-006`, `ID-I04`, `ID-L03`, `ID-T04`, the prepared-source relationship/cardinality, source grant lifetime, contained read-only tool association, source failure/cancellation mapping and their tests.
- Before task generation or any later delivery, integrate and verify Story 18.1 in this checkout, inspect its accepted spec/plan/review, production contracts, tests and verification evidence, replace the deferred source-binding assumptions, reconcile the affected spec/plan through the earliest owner, rerun deterministic validation and independent plan review, and reassess the repository planned-change triggers. Only then may the retained definition author generate tasks under a newly permitted continuation.
- No implementation tasks, source/test/migration/configuration changes, database/provider/runtime activity or implementation approval occurred.

## Planning checkpoint

- Pending correction checkpoint: `bmad-18-4-plan-correction-20260928T092111Z-d166`.
- Planned manifest: this development record, current feature definition/plan set, workflow state and current raw self-review/review/transition evidence. The ignored local `.specify/feature.json` selector is not part of the commit.

## Workflow bookkeeping

- Activation transition: `specs/017-october-batch-setup/evidence/transitions/transition-0001-activation.json`.
- Preflight: `specs/017-october-batch-setup/evidence/preflight-01.json`.
- Activation checkpoint will include only lifecycle/definition setup records; specification and plan follow under their own governed stage evidence.

## Feature directory relocation

- On 2026-09-28 the user reported that another runner had occupied sequential feature number `016` and directed this feature to move to `017`.
- The live feature mapping is now `specs/017-october-batch-setup`. Specification and plan semantics, recovery count, author ownership, raw review outcomes and the Story 18.1 hold are unchanged.
- Repository-relative evidence paths and their dependent hashes were mechanically rebound. The original exact bytes remain recoverable in commits `f693f2c5`, `acc74c51` and `30936b94`; relocation evidence is `specs/017-october-batch-setup/evidence/feature-directory-relocation-01.json`.
- Existing Git history was not rewritten. A normal rename commit is safer for later merge/rebase because it preserves stable commit identities and lets Git apply rename detection against the other runner's current tree. History rewriting would not remove ordinary overlapping-content conflicts and would make shared checkpoint references stale.
- Pending relocation checkpoint: `bmad-18-4-feature-relocation-20260928T153541Z-d166`.

## Delegated authority registration

- Verified user sources register this runner as stable chat `01a0e728-b7cc-78f1-8a3d-d0d49c7dfde2` and the October approver as stable chat `01a0e708-c489-7871-9f73-31413f20bf2c`; later title matching is not authority.
- The designation covers the two existing delegated gates and permits consideration of a fresh finite recovery-extension decision at a reached pause limit, with cumulative `max_pause_at` no greater than 7. It is not an automatic extension and does not add seven attempts.
- Story 18.4 remains explicitly PLAN-ONLY. Current recovery remains `failed_results: 1`, `pause_at: 4`; no gate, extension, task generation, implementation or resume is approved.
- Recovery-extension delegation is now compatible with the integrated governed-delivery 0.7.4 consumer policy, but it has not been invoked or approved for this episode. Any future use still requires a reached pause limit, current raw evidence and a fresh bounded delegate decision within the user's cumulative ceiling of 7.
- The exact provenance, conditions and non-effects are recorded in `specs/017-october-batch-setup/evidence/delegated-authority-registration-01.json`. Any later use requires current raw evidence and a fresh delegate response; cumulative count/limit 7 returns to the human.

## Story 18.1 baseline integration

- The user authorized merge-in-place for Stories 18.2–18.4. Root merged pinned `oct14/main` commit `f7a80cad6ac5b73a2a02ae015e4347590b6200ff`, which contains Story 18.1's completed checkpoint commit `3632b2cc5e0752b66162e71ec4da95628fe3efcd`, without rebasing or rewriting either history.
- Tracker resolution preserves Story 18.1 as `done` and Story 18.4 as `in-progress`. Project state preserves the current governed-delivery 0.7.4 and test-isolation policy together with this story's explicit plan-only hold.
- Baseline availability is now established, but dependency reconciliation is not complete. The actual accepted boundary uses `PreparedSourceApproval`, `PreparedSourceGrantRequest`, `IPreparedSourceCurrentAuthorityVerifier`, `PreparedSourceContextIssuer`, one borrowed `IPreparedSourceReadContext`, and exactly three closed Worker functions. The plan owner must reconcile ID-I04/ID-L03/ID-T04, FR-011, SC-006, data-model cardinality, grant lifecycle and current-authority behavior against those contracts.
- Integration is recorded as a plan-owner invalidation in `specs/017-october-batch-setup/evidence/story-18-1-baseline-integration-01.json` and transition 0004. Recovery remains in the same episode with `failed_results: 1` and `pause_at: 4`; no approval, task generation, implementation, database/provider/runtime work or source-access contract duplication is authorized.
- Active Story 18.4 hash-bound definition/evidence files now have an explicit LF checkout policy. This corrects cross-checkout byte materialization only; it does not rewrite historical evidence or change accepted plan meaning.
