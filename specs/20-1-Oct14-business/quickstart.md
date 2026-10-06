# OCT-BUSINESS future validation guide

Planning-only guide. These commands have not been run for this feature. Do not run hosts, migrations, provider calls or resource suites until implementation and owned runtime targets are authorized.

## Prerequisites

- Actual branch `oct14/business`, includes planning baseline `fad7f11c9d6bc9ed3da1b371b24beb9f5c09db5d`; feature path is the absolute `C:\Users\13728\.codex\worktrees\f081\vulgata\specs\20-1-Oct14-business`.
- Initial owner-published compiling contract/fixture commit available for minimal consumer compile/conformance probes. Those probes can validate CP-K1/C0 before dependent production is enabled; no completed consumer implementation prerequisite. Production/integrated scenarios below require accepted CP-K1 and all consumers on its exact commit. This planning correction implements neither contract nor probes.
- .NET/PowerShell dependencies already configured process-locally; no user/machine PATH edits. Separate lane build/test outputs through the established launcher.
- Explicit owned PostgreSQL targets and approved provider/source scope. Real Identity/System/Repository records exist; do not seed arbitrary shared data. Generator is authenticated through its existing operator route, ordinary readers through independent live Identity checks.
- Business/integration coordinate the October schema window. Web uses frozen DefaultConnection and explicit batch/publication; startup does not migrate October or require generator secrets. Starting ordinary Web can still migrate Identity/domain data, so it also needs approved targets.
- Owned append-only log path, protected keys, provider profile and total runtime budgets. No secrets in source or evidence.

## Commands after implementation

Run from the assigned worktree. Future test namespace for this feature is `Vulgata.Tests.October.Knowledge`; a zero-selected/all-skipped run is failure, not acceptance. Adjust a filter only to the implemented reviewed test names, never to hide failures.

```powershell
$env:SPECIFY_FEATURE_DIRECTORY = 'C:\Users\13728\.codex\worktrees\f081\vulgata\specs\20-1-Oct14-business'
git branch --show-current
git merge-base --is-ancestor fad7f11 HEAD
pwsh -NoProfile -File eng/test.ps1 -PolicyOnly
pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~Vulgata.Tests.October.Knowledge'
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.October.Knowledge'
```

PostgreSql creates only test-owned containers/databases through the existing launcher. Required suite traits/helpers belong in `tests/Vulgata.Tests/TestInfrastructure/suites.json`; coordinate its shared edit. Do not use ambient application/admin connections. The run is successful only with complete test outcomes and verified owned cleanup. SQLite does not qualify transaction/constraint equivalence.

For final integrated corrections, integration runs the complete SelfContained suite once with `pwsh -NoProfile -File eng/test.ps1`, plus affected PostgreSQL/native/provider/UI suites. New failures or changes justify reruns. Business separately builds its affected projects and supplies scoped evidence; historical reported 2,281/13 passes are not rerun or adopted as this feature's result.

## Usable scenarios and expected results

1. **B0 controlled:** use the labelled fixture through the production acceptance port with a test-only trusted issuer. Verify full-file/zero-unit acceptance, forged/incomplete denial, exact replay/conflict and publication isolation. No provider needed; no real generation claim.
2. **PostgreSQL correctness:** concurrent identical/conflicting writers, seal -> drain expiry -> write rejection, fence during staging versus already-submitted commit settlement, atomic file/BL rollback, exact acknowledgement recovery, cross-batch constraints, protected parent deletion and attempted immutable history mutation. One committed result or a truthful indeterminate state.
3. **B0 real:** Scan supplies its authorized run command after implementation (not invented here). Select one approved file using authenticated setup and real source/provider adapters; accept and publish the settled CL-only selection with empty relationship/flow/BL sets, no B1/B2 requirement and zero Business provider calls; Scan owns the early Generate path; integration composes Web; Explore opens the exact CL as an ordinary User. Record source/proposal/publication identifiers and observed result without raw source/secrets.
4. **Disclosure:** use IT and Business modes, ordinary User, denied/out-of-scope/deleted/moved contributor, uncited composite contributor, role/stamp revocation and authority outage. Probe content, section, search, related, count/cursor, graph and Chat footer/history. Expected zero forbidden metadata. Verify three-Repository and standalone cases have no pair/System gate.
5. **B1 real:** select the small connected two-Repository slice. Pin manifests and Scan terminal dispositions; run bounded document-only inquiry. Verify source-backed caller/provider operation and connecting binding evidence, opposing evidence and one actual unknown. Static linkage must not claim deployed execution.
6. **B2 real:** select two source-backed roots with a shared helper and a cycle/local-only control. Generate BL from exact accepted inputs. Independently compare expected conditions, alternatives and effects to source; record absent facts as gaps. Check failed drafts invisible and exact dependencies include all supplied/read evidence.
7. **Qualification and stop controls:** carry a mixed source/advisory claim through CL -> BL -> search excerpt/Chat input with its visible qualification intact. Gracefully stop analysis with accepted, active and undispatched files; settle writes/lookups and finalize the accepted subset with cancelled/unprocessed gaps. A paused or commit-indeterminate operation must block freeze. No late accepted write may change the corpus.
8. **Chat contribution:** Chat runs active-turn/terminal outcome tests against the sole schema; duplicate save is idempotent, changed outcome conflicts, uncertain acknowledgement uses lookup. Real streaming/tool-round/follow-up evidence belongs to Chat/integration, not this schema test.
9. **Combined handoff:** same publication drives real Chat/tool rounds, reader and graph. References open exact versions; related meaning/direction remains intact. Integration records final schema/build/data/configuration, actual coverage, waits/duration, limitations and start/stop instructions for the October 10 freeze.

## Focused F1/F2/F5 checkpoint probes (future, controlled)

Use deterministic barriers in the shared fixture to order seal, transaction staging, effective fence/deadline, commit submission and acknowledgement. A completed capability followed by drain expiry before writer entry creates no transaction; expiry after staging but before submission rolls back. Commit already submitted followed by hard stop/revocation must resolve exact committed or confirmed noncommitted state, or remain indeterminate; freeze cannot use a temporarily absent row while the original commit may still finish. Test first graceful stop independently from drain expiry, hard cancellation, authority outage and already committed replay. All real database claims still require later owned PostgreSQL evidence.

Inject Scan's host execution scope and a controlled provider transport, with cap=1 and two concurrent Business inquiries, then inquiry+synthesis. Assert maximum active provider attempts is one, queued work acquires only after settled permit release, no outer-orchestration permit deadlock, and shared request/usage budgets decrease exactly once. Repeat with hard stop while one call is active and one queued: active is cancelled, queued/new/retry calls never contact the provider, late response cannot newly accept/publish, and actual/unknown usage is recorded without counting unknown as zero. Check missing scope, deadline/budget denial, host rate-delayed/denied queuing with no provider send before rate admission, conservative unknown-usage reservation and fresh acquisition on retries. Rate waits retain the original deadline; no independent/reset Business limiter is permitted. Generator process-local controls are sufficient; no cross-process Web permit service.

F5 probe uses only real B0 acceptance/publication/read ports and a settled nonempty CL set, with B1/B2/provider dependencies absent. Publication must return exact readable CL handles and truthful coverage; no matching/synthesis/provider invocation occurs. Zero-unit-only returns coverage/no-readable-content rather than passing the real-CL milestone. Later BL publication remains a distinct immutable selection.

Review/conformance observations and Scan's explicit F1/F2/F5 acknowledgement must name the initial contract/fixture hash. Local Business review alone does not establish consumer concurrence. No provider, database, test code or C0 implementation is authorized in this correction session.

## Evidence and stop reporting

Report static inspection, controlled SelfContained, owned PostgreSQL, real provider/source usefulness and combined UI/runtime evidence separately. Include commit, shared-contract hash, selected database/batch/publication, schema migration, source scope, command, suite/outcome and owned cleanup result. Keep credentials/private paths/payloads out of shared evidence.

Do not invent a generator run syntax, provider profile or reader URL before their owners supply it. Missing composition, source authority, provider permission, migration window or accepted contract is an explicit dependency. These are future integration inputs, not unresolved business requirements or permission to improvise another lane's implementation.




## Phase 1–2 implementation record — 2026-10-06

The user explicitly authorized `speckit-implement` phases 1 and 2 (T001–T006), followed by a pause. This supersedes earlier session-specific planning-only text for these tasks only. No B0a store, database/schema, provider/source execution, Web composition or later increment is authorized. The current branch is `oct14/business` at the `fad7f11c` starting baseline; the canonical Markdown contract remains `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36`. Scan's planning concurrence remains as recorded in plan-review.md.

Ownership: root owns Core/Shared production contracts/validators; `checkpoint_test_owner` owns new October/Knowledge test/fixture/probe files; `checkpoint_integration_support` is the sole local project-reference/test-inventory editor for this checkpoint. Its scoped Shared->Core reference preserves one set of identities/enums without a dependency cycle. No other checkout or shared Web composition is edited. Business remains sole October schema owner; no schema changes in this checkpoint. Requirements checklist: 21 checked, 0 unchecked. No before/after implement hooks are configured; optional agent-context hooks are not run.

Runtime boundary here: compilation, repository policy checks and SelfContained controlled transports/clocks only, using owned launcher outputs. No PostgreSQL, native tools, provider, source scan, application startup or external database. Existing .gitignore/.dockerignore cover the .NET build/test outputs; no unrelated ignore/configuration changes. Fixture conformance will be labelled controlled, not production storage/live-authorization/provider proof. Later resource-suite prerequisites from the guide above remain pending.

The initial compiling contract commit is not itself accepted K. T006 additionally requires integration import and consumer acknowledgements against its actual hash; no acknowledgement or import may be invented from local probes. No cross-chat outbound instruction is inferred from this local execution request.

### Verification and checkpoint handoff

Production validation followed independent behavioral RED: `pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~Vulgata.Tests.October.Knowledge.KnowledgeRequestValidatorTests'` built/discovered current sources and ran 18 SelfContained tests: 6 passed, 12 failed, 0 skipped, in `TestResults/isolation-ad8b157eedcf4484bf531d362a1cc015/results.trx`. The earlier test compilation failure is not RED. Minimal FluentValidation implementation then made the focused October namespace GREEN: 48 passed, 0 failed/skipped, in `TestResults/isolation-1b1d824ae1984c9299a581a25822c954/results.trx` using `pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~Vulgata.Tests.October.Knowledge'`.

The independent reviewer required two contract corrections before this GREEN run: explicit pretransaction eligibility plus a synchronous callback that actually submits the database commit before returning its settlement task; and typed finalization outcomes preserving published/partial/no-content/failure/indeterminate meaning on replay. Core contains these required seams, not their production implementations. Shared references the canonical Core identities rather than duplicating them.

Final fixture review also corrected each CL source/contributor identity and identified a controlled-model counterexample: authority revoked/unavailable during staging reached submission because only fence/clock were checked at that boundary. This is a test-model correction verified by static path inspection and new guarded-submission probes, separate from production validator RED. Post-submission authority loss must continue preserving settled history while denying disclosure. Final current-source results and disposition follow below.

Final current-source verification: `pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~Vulgata.Tests.October.Knowledge'` discovered 3,379 SelfContained cases and selected 50: **50 passed, 0 failed, 0 skipped**. TRX: `TestResults/isolation-c75b5376643d4318ba02fad129924bd1/results.trx`. Source policy covered 320 files; the launcher's policy/outcome/timeout/cleanup/fixture-guard probes passed. The preceding ClC-corrected run also passed 48/48 in `TestResults/isolation-cab62665512f45db8c76a380760a138c/results.trx`; the final 50-case run adds both staging authority-loss cases. Root checked persisted RED/final-GREEN counters and `git diff --check`. No full combined suite, PostgreSQL, native, provider or application runtime qualification is claimed.

Independent reviewer `independent_plan_review` found no remaining actionable findings for T002–T005 and accepted the local candidate conditional on this final 50-case GREEN; that condition is now satisfied. Separate test/production owners accept the final scoped shape. Canonical Markdown contract SHA is unchanged. No before/after implement hooks are configured. No root instructions, optional agent-context hooks, global settings, shared Web composition, other checkout, database or unrelated process was changed.

T001–T005 are complete. T006 has only its local candidate preparation fulfilled: integration must import the actual commit below and Scan must acknowledge completion/guard/scope probes, with Chat/Explore acknowledging exact read-handle probes against the same imported boundary. These are dependency/conformance checks, not additional human approval gates. Actual accepted K remains **pending** until those external facts are supplied. Existing Scan planning concurrence is not a compiled consumer acknowledgement. No tool surface, Scan strategy or PRD scope beyond the accepted phase-2 contracts was introduced; changed future scope/resource targets retain the documented approval triggers. Execution pauses here before T007 or any phase-3 implementation.

Staged diff inspection reported only extra blank lines at EOF in newly tracked planning/code files. These are nonbehavioral whitespace findings; accepted planning artifacts and their recorded hashes were preserved. No formatter/build policy was changed or disabled.

Local compiled contract/fixture candidate: **`07c3fe4463e3228a6982fd106b58148ca87974bd`**, parent `fad7f11c9d6bc9ed3da1b371b24beb9f5c09db5d`, on `oct14/business`. This is the exact source/test boundary verified above. Integration import: pending. Scan compiled completion/guard/scope acknowledgement: pending. Chat/Explore compiled read-handle acknowledgements: pending. **Accepted K: not yet established.** The following documentation-only handoff commit records this immutable candidate; neither commit was pushed or merged elsewhere.
