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
