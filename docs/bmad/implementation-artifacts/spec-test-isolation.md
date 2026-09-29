---
title: 'Self-contained default test execution'
type: 'bugfix'
created: '2026-09-29'
status: 'done'
route: 'dispatch'
baseline_commit: 'd99f6a0f71a7c20b36b68e462efe9b0a552578c7'
review_loop_iteration: 0
context:
  - 'G:/source/repos/vulgata/docs/agents/testing.md'
  - 'G:/source/repos/vulgata/docs/agents/development.md'
  - 'G:/source/repos/vulgata/.specify/memory/constitution.md'
  - 'G:/source/repos/vulgata/docs/research/test-isolation-audit-2026-09-29.md'
---

<frozen-after-approval reason="User approved the audit plan, implementation sequence, and then instructed Resume">

## Intent

Ordinary feature automation currently runs tests that require external services,
personal tool installations and historical checkpoints. Make the default test
suite self-contained and enforce that boundary for future tests, while retaining
required real-service qualification in explicit suites.

## Boundaries & Constraints

Always retain the single xUnit 2.9.3 test project, existing assertions, real-provider
qualification, and truthful outcomes. Classify indirect fixture dependencies.
Resource selection and ownership must be checked before external side effects.
Never use application credentials or a pre-existing shared database, weaken
production authorization, modify product source, or rewrite historical evidence.
Do not change portable workflow skills, lifecycle state or sprint tracking.
The existing edits in AGENTS.md, adoption-0.7.md and project-state.md are unrelated:
preserve them; only add the narrowly needed testing pointer/state update.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior |
| --- | --- | --- |
| Default | No services or credentials | Complete SelfContained suite runs without external resource acquisition |
| Direct runner | Unfiltered CLI/IDE or focused external test without opt-in | External cases excluded/skipped before fixture side effects |
| Policy violation | Unclassified test or forbidden self-contained dependency | Actionable nonzero policy result |
| Empty selection | Focused filter matches no tests | Nonzero runner result |
| Explicit suite unready | PostgreSql/tools/provider requirements absent | Non-success prerequisite result, never an early-return pass |
| Owned PostgreSQL | Explicit suite and disposable resource scope | Migrate/test/cleanup only resources created by that run |
| Ambient configuration | Existing or bogus service configuration | Default remains self-contained; no implicit execution authorization |

</frozen-after-approval>

## Code Map

- `tests/Vulgata.Tests/Vulgata.Tests.csproj`: single net10 project; no default filter.
- `.governed-delivery/config.toml`: unfiltered commands.test consumed by final gate.
- `tests/Vulgata.Tests/KnowledgeStorePostgreSqlConcurrencyTests.cs`: four tests delete supplied target; replace setup with owned fixture.
- `tests/Vulgata.Tests/Migrations/DisposablePostgreSqlDatabase.cs`: reusable unique-database helper; harden run ownership and setup guards.
- `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureApplicationFixture.cs`: hidden PostgreSQL fanout; trace consumers.
- `tests/Vulgata.Tests/LoginLogoutTests.cs`: reusable SQLite/TestServer replacement of both contexts.
- `tests/Vulgata.Tests/AnalysisRuntime/RecordingProcessLauncherDouble.cs`: native-process double.
- `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs`: historical checkpoint suite.
- Audit in context lists mandatory configuration, early-return passes, custom attributes, real tools, nested processes and safe patterns.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Vulgata.Tests/TestInfrastructure/` and test files: classify the complete test inventory, including helpers and mixed files; implement shared suite metadata and discovery/execution guards. Conservative file-level external classification is acceptable initially when documented; preserve every assertion. Keep test discovery inert.
- [x] `tests/Vulgata.Tests/Vulgata.Tests.csproj` and runsettings: safe direct CLI/IDE default plus opt-in guards that filter overrides cannot bypass.
- [x] `eng/test.ps1` and policy checks: default SelfContained, explicit PostgreSql/LocalTools/External/E2E/HistoricalBoundary, composable focus filters, classification validation, current build, truthful TRX counts, failure propagation and empty/all-skipped rejection.
- [x] PostgreSQL fixtures and consumers: remove direct deletion of supplied targets, centralize explicit ownership/selection, normalize missing prerequisites. Provide an explicit disposable PostgreSQL 17 runner boundary, preferably owned container provisioning; never connect to ambient services. Preserve child-process contracts and multi-resource requirements.
- [x] Policy regression tests: prove selection, guard, classification, missing readiness, empty result, and ownership behavior with controlled probes; no real shared database needed.
- [x] `.governed-delivery/config.toml`, `docs/agents/testing.md`, `docs/agents/development.md`, `AGENTS.md`, `docs/agents/project-state.md`: adopt runner and enforced authoring rules; keep required feature-specific E2E/provider gates.

**Acceptance Criteria:**
- Given ordinary SDK/package prerequisites and no services, when the default runner executes, then all classified self-contained tests pass with reported counts and excluded qualification clearly identified.
- Given new test files or methods and indirect helpers, when policy validation runs, then missing classifications or prohibited dependency paths fail rather than silently disappear.
- Given an explicitly selected external suite with no prerequisites, when invoked, then it cannot report successful qualification.
- Given an owned disposable database, when setup/cleanup runs, then only that run's resources can be changed; required PostgreSQL assertions remain intact.
- Given unrelated feature work, when governed verification runs, then unrelated external/historical checks do not block it; applicable qualification still remains required.

## Implementation Notes

Approval provenance: user said "Approve your plan. How are you going you implement
them?" after the audit, then "Resume" after the four-stage implementation sequence.
No new product-story activation is needed for this infrastructure repair. Retain
this approval for unchanged scope; do not request duplicate approval.

Sequential implementation; the implementation agent owns test infrastructure,
tests, runner, config and narrow documentation edits listed above. It is not alone
in the checkout: preserve other edits. Root owns spec status, review records and
Git; no subagent commits or stages. Do not invoke bmad-build recursively. Make local
implementation choices within this contract and report concrete blockers honestly.

## Spec Change Log

## Review Triage Log

Review 1: independent blind, edge-case and verification-gap reviewers inspected
the implementation diff. Each finding was assessed before grouping:

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Blind 1 anonymous volume cleanup | high | Runner removes container without volumes; postgres image owns an anonymous data volume. Patch owned cleanup and verify mounts are removed. |
| Blind 2 seeded T062 qualification unreachable | high | New owned-target check rejects ambient inputs, but runner provides no prepared owned target. Patch an explicit local seed import into the owned container; keep provider consent flags. |
| Blind 3 socket readiness race | medium | pg_isready lacks host, so initialization socket can satisfy readiness before final TCP server. Patch TCP check. |
| Blind 4 unsupported WithPostgreSql combinations | medium | Runner provisions for SelfContained/LocalTools/HistoricalBoundary although scope rejects them. Reject before provisioning. |
| Blind 5 interpolation hides forbidden calls | medium | String-strip regex removes interpolated executable expressions. Patch lexical inspection and mutation probe. |
| Blind 6 target-typed HTTP constructor | medium | Return expression new() is not covered by direct-construction patterns. Patch detection and regression. |
| Blind 7 unconstrained exemption regex | medium | Manifest patterns can erase entire files without validated rationale. Replace with bounded reviewed exemption identifiers. |
| Blind 8 unbounded setup processes | medium | Test hang timeout does not cover build/discovery/Docker calls. Bound owned child processes and preserve cleanup on timeout. |
| Blind 9 unexpected SelfContained skips | medium | Runner accepts any default-suite skip despite requiring explicit documented omissions. Apply explicit skip policy to every suite. |
| Blind 10 policy regression scripts outside default gate | medium | Scripts exist but standard command does not execute them. Wire economical policy/outcome/fixture probes into normal verification. |
| Edge 1 combined attributes bypass policy | high | Legal combined attributes evade both counters and bare-attribute rejection. Validate every attribute list and guarded metadata, with regression. |
| Edge 2 seeded T062 qualification unreachable | high | Same verified cause as Blind 2; grouped for one patch. |
| Edge 3 T124 ambient administration | high | CloneAsync opens supplied T124 admin and creates a database before scope validation. Validate admin/source first, record successful clone ownership. |
| Verification 1 unrecorded target regression missing | medium | Existing unit cases only protect admin endpoint checks. Add recorded/unrecorded target tests without connections. |

All findings route to contained implementation corrections under the already
approved resource isolation and future-test enforcement intent. Preserve the
passing suite migration and existing assertions; no product behavior or provider
execution is authorized by these corrections. Required seeded qualification inputs
remain user-provided; do not manufacture source manifests or copy shared databases.

Review 2: the retained edge reviewer confirmed all three original findings resolved
and found no further concrete process/cleanup scope issue. One additional medium
finding is patched: an object initializer can omit HttpClient constructor
parentheses, producing a null syntax ArgumentList; treat it as zero arguments and
add a policy mutation probe. This preserves the existing controlled-transport rule.

## Verification

### Observed completion evidence (2026-09-29)

- Final standard command `pwsh -NoProfile -File eng/test.ps1`: **1,856 passed,
  0 failed, 0 skipped**. 271 source/helper files classified. Current-source build:
  0 errors, 17 warnings. Local evidence:
  `TestResults/isolation-af4c0237989a4fcca28849fa6088dcd6/results.trx`.
- Final owned PostgreSQL smoke: **6/6 passed**, covering the formerly destructive
  KnowledgeStore concurrency tests, shared disposable helper and retained-source
  migration. Local evidence:
  `TestResults/isolation-9527c927febd41abac13494651331e2a/results.trx`.
  Container and run-labeled data volume removed; mount/cleanup/volume-absence logs
  confirm that boundary.
- Earlier broader fixture/child-process sample: **51/51 passed** at
  `TestResults/isolation-a0a7e1860c4c4a1ca83af440f3f242cf/results.trx`.
- Review-fix ownership/metadata tests: **10/10 passed** at
  `TestResults/isolation-06d028baaa614dd3ad207fe498137b3f/results.trx`;
  all are included in final default verification.
- Controlled seed import and T124 clone probes: **2/2 passed** at
  `TestResults/isolation-4f713ecbd7fc41fda64417e41f868ec5/results.trx`;
  both probe containers and labeled volumes were removed and absence verified.
- Policy mutations, exact skip allowlist, native timeout/tree termination,
  sensitive-output redaction, cleanup failure/environment restoration and
  disabled-fixture/control probes pass through the normal command.
  The enabled sentinel intentionally fails as the positive control; that expected
  probe failure is distinct from the default suite's zero failures.
- Actual empty-selection and missing-prerequisite wrapper probes returned exit 1.
  Evidence respectively:
  `TestResults/isolation-255c78f56c9846179d1235bea37d35d5/results.trx` and
  `TestResults/isolation-4487c2843540482bb9acde33f6fa00e9/results.trx`.
- Independent review findings were corrected; retained edge review confirmed its
  original findings resolved. Its final HttpClient initializer finding has a
  passing mutation regression and is included in final default verification.
- Governed-delivery dependency/configuration check passes with no warnings.
  The aggregate historical workflow validator remains non-green: **223 findings**
  in this checkout, recorded in `TestResults/test-isolation-workflow-validation.log`.
  Historical states/evidence were not rewritten, and no story was activated.
- No product-source edits or removed test assertions. Pre-existing AGENTS,
  adoption and project-state edits were compared with their captured starting
  content and preserved. Whitespace checks pass.

Evidence files under TestResults are local ignored execution output; the commands,
results and scope are retained here. These results do not qualify every PostgreSQL
case, native tool, real provider, E2E or historical suite. Runtime OS-level network
denial was not established. Conservative file-level classification can place safe
methods from mixed files in an explicit suite; no assertions were discarded.

### Repeatable verification scope

- Run policy/runner regressions, then build and the complete default suite from current sources with service environment removed.
- Probe direct/focused CLI selection with a controlled sentinel external fixture, proving no fixture initialization when disabled.
- Exercise explicit missing-prerequisite and zero-selection failure paths.
- Run owned PostgreSQL integration only if an isolated disposable resource can be provisioned within this scope; otherwise report the concrete runtime blocker and retain incomplete qualification.
- Use network-denied execution where the host supports it; distinguish static policy checks from demonstrated runtime containment.
- Review full task diff, links and whitespace; report existing warnings separately.
