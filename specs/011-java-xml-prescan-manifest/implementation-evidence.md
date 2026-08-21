# Story 12.2 Implementation Evidence

This durable record supplements the compact workflow state. It records observed
RED/GREEN evidence and checkpoint-qualified task groups; it does not replace the
accepted specification, plan, tasks, or review verdicts.

## Group 1: T001-T027

- Boundary/support tasks T001-T005 and T012: test-project build completed with
  zero errors; compatibility-focused suite passed 15/15.
- Canonical identity and selection T006-T011: the focused RED was 13 failed,
  0 passed. The same focused suite passed 13/13 after GREEN; the surrounding
  Code Unit/audit/identity/pass-flow suite passed 42/42; Core built with zero
  errors.
- Exact revision, inventory, raw artifacts, and audit T013-T021: the focused RED
  was 8 failed, 0 passed. Focused plus existing exact-Git coverage passed 17/17;
  Infrastructure built with zero errors.
- CodeGraph and JDT-LS readiness T022-T025: the focused suite passed 6/6 after
  GREEN and the Story 12.1 surrounding analysis suite passed 12/12. A broader
  CodeGraph qualification run passed 102 tests and failed 2 pre-existing
  workflow-fixture checks because `plan_revision` was absent; neither failure
  exercised the prepared readiness production paths.
- Java split ranges T026-T027: the focused RED was 3 failed, 0 passed because the
  prepared provider/range seams were absent. The same suite passed 3/3 after
  GREEN; surrounding canonical/range/raw-artifact coverage passed 18/18;
  Infrastructure built with zero errors.
- Root combined verification through T027 passed 28/28 Story 12.2 boundary,
  canonical, selection, acquisition, raw-artifact, audit, CodeGraph-readiness,
  and Java-readiness tests.
- `git diff --check` passed. NU1507, existing analyzer warnings, and existing EF
  assembly-version conflict warnings are reported separately from errors.

The approved runtime and scope remain unchanged: Vulgata, CodeGraph 1.5.0,
Java 21, and JDT LS 1.61.0 run on the Windows host; PostgreSQL 17 is the only
required Docker service; CodeGraphContext and Story 12.3 Worker execution or
Document generation are absent.

## Group 2: T028-T035

- Safe XML, XML split, catalog, and Communication Kind Classification began at
  11 failed and 2 passed tests because the production seams were absent.
- The initial four-class GREEN suite passed 13/13; combined US3 plus Java range,
  canonical, and raw-artifact surroundings passed 26/26; Infrastructure built
  with zero errors.
- Independent review found that the accepted immutable embedded catalog behavior
  still lacked its resource. The original test author added a public-seam
  `LoadEmbedded()` assertion and observed 1 failed/3 passed with the expected
  unavailable-resource exception.
- After adding the neutral closed-grammar catalog resource and exact logical-name
  project entry, the original test author independently verified catalog 4/4,
  complete US3 14/14, and communication/range/raw-artifact surroundings 64/64.
- The embedded resource SHA-256 is
  `d8cb15b874628264cb767b4fb93e5b7d66a1b5d12e170532c49f957324d08d79`.

## US4 RED baseline

- T036, T038, T041, and T043 test-only work builds with zero errors. The combined
  focused baseline is 13 failed and 3 passed, identifying missing File Code Unit
  construction/expansion, stable prepared handoff, durable store/replay, and
  paired preparation API/coordinator seams.

## Group 3: T036-T039

- T036 File Code Unit/selection moved from three missing-seam failures to 4/4
  GREEN; canonical/selection/audit/readiness/range surroundings passed 28/28;
  Infrastructure built with zero errors.
- T038 stable prepared context/handoff was independently reconfirmed at 2 failed
  and 2 passed, then reached 4/4 GREEN; legacy context/Pre-Scan/canonical
  surroundings passed 76/76; Core and Infrastructure built with zero errors.
- The original US4 test author independently verified T036 plus T038 at 8/8 and
  canonical/selection surroundings at 13/13. T041/T043 remain honest RED at
  8 failed/0 passed pending their store and API production owners.
- A broader historical `PreScan` filter passed 96 tests and retained one unrelated
  pre-existing Story 4.3 composition failure; it is outside this narrowed group.

## Group 4: T040-T046

- T040 added the cohesive prepared-scan EF migration and model. The focused
  model/context/handoff suite passed 23/23; API surroundings passed 31/31 after
  a migration-only SQLite ordering bridge corrected a temporary-table foreign-key
  mismatch. Infrastructure and Web built with zero errors, EF reported no pending
  model changes, and the PostgreSQL idempotent script excluded the SQLite bridge.
- T045 executed two PostgreSQL 17.10 migration lifecycle tests on a fresh
  disposable service. Clean apply, rollback, protected historical/v1/demo/Story
  12.1 compatibility, reapply, and forced late-boundary rollback all passed 2/2;
  no owned databases remained and the disposable container was removed.
- T041/T042 reached 5/5 focused GREEN for exact-two atomic commit, exact replay,
  mutation conflict, cancellation/rejection zero-write behavior, and durable
  manifest/entry/Code Unit/audit/result/context persistence. Subsequent
  requirement-backed regression coverage passed 3/3 with one justified
  PostgreSQL-only skip under SQLite after full canonical reconstruction,
  classification persistence, stored Code Unit fingerprint validation, and
  concurrent identical-writer recovery were corrected.
- T043/T044 API composition passed 5/5 focused, 14/14 API/store/handoff, and
  31/31 surrounding API tests. Core, Infrastructure, Shared, and Web built with
  zero errors. T047 first exposed that the production builder silently skipped
  oversized XML and emitted no classifications. The unchanged release tests moved
  from 1/3 to 3/3 after the builder was corrected to use safe XML parsing,
  direct-child range planning, catalog evaluation, and bounded classification.
  The original test author independently reran the exact 3-test filter GREEN in
  46 seconds; the test SHA-256 remained
  `fbec83be1ffaf9f3b788f144a0a10ca409c45faeb56158b7679f43ad696dcb52`.
  Per-Repository PreScan under 120 seconds, CodeGraph under 30 seconds, and JDT-LS
  under 60 seconds assertions all passed. T044 and T047 are therefore complete.
- T046 real host-tool qualification passed 5/5 using exact Git, CodeGraph 1.5.0,
  Java 21, and JDT LS 1.61.0. Deterministic readiness/proof coverage passed 8/8
  and Story 12.1 adapter surroundings passed 11/11. XML-only readiness is explicit
  NotApplicable; hard failures remain closed; related-file expansion remains the
  approved warning fallback.

## Group 5: T048-T051 qualifications completed to date

- T048/T051 workflow, runtime-pin, security, and scope boundary tests passed 4/4.
  An independent scan inspected 28 prepared production files and found zero
  forbidden Worker, Document, LLM, CodeGraphContext, or Story 12.3 surfaces.
- T049 production-literal qualification passed 1/1 twice with byte-identical
  output. It inspected six production assemblies, nine embedded resources, and
  one public catalog projection; the denylist found zero matches. The required
  ignored JSON artifact is retained at
  `artifacts/qualification/story-12.2/production-literal-inspection.json`.
- T050 authoritative PostgreSQL 17.10 qualification discovered and passed 21/21
  tests in 16 seconds. It covered six forced-insert boundaries, six cancellation
  boundaries, stale and concurrent writers, exact replay/conflict, stable proof
  and selected-child mutation, stored Code Unit fingerprint tamper, retained
  classification persistence, audit reconstruction, and null/FK enforcement.
  No harness-owned databases remained; the disposable container was removed and
  the existing project PostgreSQL service was untouched.

## Group 6: T052 final verification barrier

- The ordered quickstart boundary passed: PreparedScanning 97/97 with one
  justified SQLite-only skip; audit 2/2; canonical/replay 13/13; real Git,
  CodeGraph, JDT-LS, and prepared analysis 13/13; PostgreSQL migration 2/2;
  PostgreSQL atomicity 21/21; API 5/5; release qualification 3/3; boundary/XML
  security 7/7; and production-literal qualification 1/1.
- The retained JDT-LS real qualification exposed deterministic empty
  `textDocument/implementation` evidence after general navigation succeeded.
  An implementation-only 250 ms retry, bounded by the existing request timeout
  and caller cancellation, moved the unchanged RED to three fresh host GREEN
  repetitions; JDT/prepared-real surroundings passed 10/10.
- The older durable-scan PostgreSQL rollback fixture was updated to seed only its
  historical columns and to expect Story 12.1 then Story 12.2 as the exact
  pending migration sequence. Its live PostgreSQL test passed 1/1 and the Story
  12.2 lifecycle suite remained 2/2 GREEN.
- Web build completed with zero errors and nine existing NU1507/MSB3277 warnings.
  The complete test project ran 1,632 tests: 1,625 passed, 6 unrelated baseline
  failures, and 1 intentional SQLite-only skip. The six preserved baselines are
  Story 4.3 runtime composition, stale sequential migration equality, two
  CodeGraph `plan_revision` fixtures, a missing legacy provider-contract image,
  and one management-page label assertion. Every Story 12.2 suite passed.
- The regenerated production-literal artifact reports six assemblies, nine
  resources, one public output, zero matches, and verdict `zero-match`; SHA-256
  `d25de28326be5edf4c700e15a3b07b857618ffe7bd84dbee46b059dd994e08a5`.
- No owned PostgreSQL databases remained, the disposable T052 container was
  auto-removed, and the existing project PostgreSQL 17 service remained healthy
  and untouched.

## Verified implementation checkpoint 2

- Checkpoint ID:
  `bmad-12-2-implement-20260820T162725Z-0029b3`.
- Parent checkpoint: `24bb73e6ad4cf2c2a48168e6f57ba97ad74eecaf`.
- Intended content is the complete explicit Story 12.2 owned production, EF
  migration/model/configuration, API/coordinator, qualification tests, literal
  JSON artifact, task markers, workflow state, and evidence boundary accumulated
  after the T001-T039 checkpoint. It excludes ignored build outputs, disposable
  database/container state, configured project data, and unrelated user paths.
- Binding is pending the root-owned local commit with the matching workflow
  trailer; convergence begins only after the trailer resolves uniquely.

## Convergence correction: T053-T055

- Convergence iteration 1 assessed all 43 Functional Requirements, 9 Success
  Criteria, 16 acceptance scenarios, 9 Architecture Decisions, and 5
  Constitution principles. It appended three traceable HIGH partial tasks for
  production Java splitting, related-file expansion, and complete inventory
  audit projection; no intent or authority defect was found.
- T053 contract RED first proved the absence of a revision-bound exact Java
  symbol-range source. After the closed `IPreparedTopLevelSymbolSource` seam was
  added, the production-bound oversized-Java behavior RED observed zero units.
  GREEN now consumes exact JDT-LS UTF-16 top-level spans through
  `PreparedJavaRangeProvider`; exact hashes/ranges/segment ordinals are retained,
  while missing, malformed, or oversized spans create one closed whole-file
  `Skipped/InvalidSplitRange` audit with no partial units.
- T054 RED observed no prepared related-file requests. GREEN invokes expansion
  only after hard readiness with canonical at-most-20 seeds,
  Calls/Imports/Inherits, two hops, 200 files, and the five-second bound; final
  score/reason updates and deterministic unavailable/timeout Warning fallback
  are verified per Repository.
- T055 RED observed no filtered inventory audit. GREEN maps filtered, skipped,
  unsafe XML, safe-reader bounds, and invalid split outcomes through the sole
  canonical audit factory and emits conservative bounded Warning
  classifications/counts.
- The exact convergence group passed 3/3. Legacy Java range compatibility plus
  the convergence group passed 7/7. Java range/readiness,
  expansion/selection, XML security/ranges/classification, release, API, and
  real-readiness surroundings passed 43/43 in 1 minute 12 seconds.
  Infrastructure and Web built with zero errors; warnings remained existing
  analyzer/package/reference warnings. `git diff --check` passed.

## Post-convergence T052 barrier

- Corrected PreparedScanning passed 101 tests with one intentional SQLite-only
  skip. Audit passed 2/2; canonical/replay 13/13; real exact-Git, CodeGraph,
  JDT-LS, and prepared analysis 13/13 in 14 seconds with host access;
  PostgreSQL migration passed 2/2 and atomicity/replay/concurrency passed 21/21;
  API passed 5/5; release plus convergence qualification passed 7/7 in 72
  seconds; boundary/XML passed 7/7; production-literal passed 1/1.
- Web built with zero errors and nine existing NU1507/MSB3277 warnings. The
  complete test project ran 1,636 tests in 2 minutes 46 seconds: 1,629 passed,
  the same six unrelated baseline failures remained, and one intentional
  SQLite-only skip remained. Every Story 12.2 test passed.
- The regenerated production-literal artifact inspected six production
  assemblies, nine embedded resources, and one public output with zero matches;
  SHA-256 is
  `ddecfbe6da9744cec962d1f051ee550beb91fdbe12516ee46029ef078a885d79`.
- `git diff --check` passed. The fresh disposable PostgreSQL 17 container and
  its database were removed; the existing `vulgata-postgres` service remained
  healthy and untouched.

## Convergence correction: T056

- Convergence iteration 2 found that production still registered the fail-closed
  `UnboundJavaAnalysisSessionFactory` and the inventory-required Java readiness
  path. The requirement-backed RED used two real local Git repositories and the
  production paired builder; it failed 0/1 with
  `prepared-scan-java-not-ready` before a Prepared Scan Pair Package existed.
- Production now resolves the scoped real `JdtLsAnalysisSessionFactory` through
  the retained `PreparedScanRevisionAcquirer` lease. Repository, Scan Run,
  Profiled Revision, and lease identity are checked before use and again during
  disposal. The exact retained revision root is the JDT `rootUri`; Eclipse
  `-data`, the lock, and copied configuration remain in a separate contained
  runtime workspace.
- Java readiness selects the ordinal-first non-descriptor Java source, opens it
  with bounded strict UTF-8 `didOpen`, requires nonempty document symbols within
  the lesser of the Scan deadline and 60 seconds, and emits the canonical pinned
  Java 21/JDT-LS 1.61.0/StreamJsonRpc 2.25.29 stdio proof. XML-only preparation
  remains explicit `NotApplicable`.
- The corrected host fixture contains an approximately 80 KB Java source with
  two bounded top-level types, proving exact JDT-LS top-level range acquisition
  and split-unit projection through the production paired builder. The unchanged
  focused qualification passed 1/1 in 40 seconds; XML-only `NotApplicable`
  passed 1/1; JDT-LS adapter/real qualification, prepared readiness, Java
  split/range, paired-real, and oversized-Java surroundings passed 19/19 in
  1 minute 12 seconds.
- Infrastructure and Web built with zero errors. Existing warning totals were
  two and 709 respectively. `git diff --check` passed with line-ending notices
  only.

## Convergence correction: T057

- Convergence iteration 3 identified F5: a raw-artifact rejection escaped the
  per-file boundary and caused `PreparedSnapshotSource` to return
  `prepared-snapshot-read-failed`, losing otherwise safe files. The authorized
  behavioral RED used one safe Java file and one binary Java file in the same
  exact local Git revision; it failed 0/1 because no retained inventory existed.
- The raw reader now emits typed closed rejection reasons for hard bounds,
  binary or invalidly encoded Java, unsupported/conflicting XML encoding,
  nonregular/unreadable files, and link/reparse entries. Snapshot inventory
  retains one noneligible outcome with bounded byte metadata, empty raw bytes,
  and null hashes, then continues deterministic processing of safe files.
  Snapshot identity changes and read races remain repository-level failures.
- `HardBoundExceeded` projects as a bounded Warning; other closed raw-artifact
  reasons retain their exact filtered audit reason. Rejected artifacts cannot
  become File Code Units or acquire selection associations.
- The unchanged focused test passed 1/1 in 907 ms, the production-bound
  `BinaryJava` characterization passed 1/1 in 6 seconds, and the combined raw
  artifact plus release suite passed 10/10 in 53 seconds. The original test
  author independently confirmed GREEN with unchanged assertions and unchanged
  test-file hashes.
- Infrastructure and Web built with zero errors and only two NU1507/nine
  existing warnings respectively. `git diff --check` passed with line-ending
  notices only.

## Post-T057 final verification barrier

- With host access to the pinned JDT-LS archive and a fresh disposable
  PostgreSQL 17 container, PreparedScanning passed 102 tests with one intentional
  SQLite-only skip. The exact PostgreSQL migration lifecycle remained 2/2 and
  atomicity/replay/concurrency remained 21/21. Retained real Git, CodeGraph CLI,
  Java 21, JDT-LS, and paired preparation qualification passed 19/19.
- The complete test project ran 1,638 tests in 3 minutes 17 seconds: 1,631
  passed, the same six unrelated baseline failures remained, and one intentional
  SQLite-only skip remained. Every Story 12.2 test passed.
- The real scoped Java factory exposed one stale synchronous-scope disposal in
  `AnalysisRuntimeHealthTests`. The test now uses `CreateAsyncScope` and
  `await using`; all four registration assertions are unchanged, the exact test
  passed 1/1, and the class passed 12/12.
- Web built with zero errors and nine existing package/reference warnings. The
  regenerated production-literal artifact inspected six assemblies, nine
  embedded resources, and one public output with zero matches and verdict
  `zero-match`; SHA-256 is
  `95e63dbaec3c9573285f8150c648188ab2fec9b353b62aacf102fef01e20f0f5`.
- The disposable PostgreSQL container and its database were removed. The
  existing `vulgata-postgres` service remained healthy and untouched.

## Convergence correction: T058-T059

- Convergence iteration 4 identified two HIGH partials. The T058 production
  paired qualification used two exact local Git revisions with real production
  DI and failed 0/1 at `prepared-scan-codegraph-not-ready`; the registered
  CodeGraph adapter had no revision-bound resolver and the paired builder had
  not run fresh index/rebuild/status before proof. The T059 qualification used
  a lexically first empty Java file and failed 0/1 because both Repository
  readiness requests retained that empty path.
- Production CodeGraph is now scoped to the preparation lease. It verifies the
  Repository, Scan Run, Profiled Revision, runtime, and opaque lease binding,
  verifies the retained exact revision before each phase, copies verified source
  into a disposable mutable analysis workspace, and rehashes every non-index
  byte before operations. The paired path runs init, rebuild, explicit complete
  status, and only then the canonical completeness proof. Hard failures still
  reject approval; only the already accepted post-readiness related expansion
  may fall back.
- Empty, BOM-only, and whitespace-only Java artifacts are excluded before the
  ordinal readiness ordering. Valid nonempty Java remains eligible and XML-only
  preparation remains explicit `NotApplicable`.
- The unchanged T058 real-host test passed 1/1 in 1 minute 12 seconds; T059
  passed 1/1 in 13 seconds. The original test author independently verified the
  five-test three-run determinism, oversized XML, XML-only, hard-readiness, and
  related-expansion surroundings at 5/5 in 39 seconds. A combined production
  run passed 5/5 in 2 minutes 14 seconds, and pinned CodeGraph/JDT-LS, Java
  ranges, and hard-readiness surroundings passed 4/4 in 1 minute 18 seconds.
- Infrastructure and Web built with zero errors and only two/nine existing
  warnings. The test project built with zero errors and ten existing warnings.
  `git diff --check` passed with line-ending notices only, and the correction
  added no CodeGraphContext, Worker Agent, Document, or `navigate_code` surface.

## Quality-review correction: QR-001

- Quality review attempt 1 found that durable replay trusted opaque CodeGraph
  and Java proof hashes and omitted canonical manifest scalars. Five stored
  SchemaIdentity/count tamper cases returned `Replay` instead of `Conflict`,
  while the schema lacked typed CodeGraph `LogicalIndexBinding` and Java
  `WorkspaceBindingHash` preimages. The corrected behavioral suite observed
  seven PostgreSQL product REDs with the original valid caller package intact.
- Prepared Scan Manifest persistence now retains every stable field from the
  versioned CodeGraph completeness and Java readiness canonical envelopes as
  typed columns. Replay reconstructs both proof fingerprints from durable typed
  state, compares stored and supplied proof identities, reconstructs the full
  manifest envelope from stored scalars and ordered relational children, and
  returns zero-mutation `Conflict` for any mismatch. Only documented volatile
  duration/telemetry and time-derived capture identifiers remain outside replay
  identity.
- The unchanged seven tamper tests passed 7/7 on PostgreSQL 17.10. The complete
  PostgreSQL atomic suite passed 28/28, SQLite atomic replay passed 5/5, and the
  PostgreSQL migration lifecycle passed 2/2. Each tamper retained the deliberate
  corrupt value and unchanged durable row counts after replay rejection;
  duration-only replay remained GREEN.
- EF reported no pending Vulgata model changes, the idempotent migration script
  generated successfully, Infrastructure and Web built with zero errors and
  only two/nine existing warnings, and `git diff --check` passed.
