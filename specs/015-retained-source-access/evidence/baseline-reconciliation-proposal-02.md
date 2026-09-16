# Proposed additional baseline reconciliation

Story 13.1 remains in implement. T001–T040 are verified; the complete retained-source group passes159 tests with4 explicit child-only skips. The required full repository run has exposed additional legacy baseline/fixture failures. No gate waiver is proposed. The earlier approval covered exactly four obsolete Repository API tests and one label assertion; it does not cover this proposal.

## Requested scope

Authorize bounded baseline maintenance needed to make the required full suite meaningful on the current repository, without activating another story or changing historical product behavior:

1. Preserve exact pre-edit originals and hashes for every touched baseline file under this feature's evidence/history directory.
2. Preserve canonical Git LF bytes for the embedded Code Logic schema and Pass1 prompt resources through narrow EOL rules. The schema's existing accepted hash matches the LF Git blob. Reconcile only stale test/fixture prompt hash literals with the current canonical Git prompt bytes; do not edit prompt prose, tool contracts, schema semantics or runtime validation to accept arbitrary hashes. If a runtime authority change is required beyond byte stability, stop and present it separately.
3. Correct legacy owned PostgreSQL fixtures that issue unqualified LogicalDocuments SQL although migrations place that table in the vulgata schema. Keep production schema and migrations unchanged.
4. Replace fixed historical migration-inventory assumptions with explicit assertions for the historical prefix plus the current additive migration where appropriate. Scope the Story12.5 historical migration-diff gate to its named accepted boundary so it does not forbid unrelated later migrations. Preserve its assertions within that historical boundary.
5. Archive and retire only the remaining DashboardPanelTests.RepositoryDetailDialog_AfterRepresentativeProfiling_RendersEveryObjectiveFactAndChineseLabel test that requires the removed private RepositoryProfilingCoordinator.Map method, if it cannot be expressed through an existing current public seam. Do not restore decommissioned profiling.
6. Investigate the Navigate security test's expected13 count against the actual12-case list; restore a missing required case, or change the count only if every accepted case is already represented; correct the historical DataProtection fixture's application-name isolation; route AnalysisRuntime Web startup through its own disposable owned database/configuration rather than the default5432 connection.
7. Make the external T048 qualification test use the test framework's supported explicit opt-in skip when authorization flags are absent. Do not enable those flags, call real providers, or count a skip as external qualification.
8. Diagnose remaining cancellation/timeout failures after repairing the preceding shared fixture blockers. Test-harness corrections may preserve the intended assertions; any production behavior change, new authority choice, or additional obsolete-test removal returns for a separately bounded decision.
9. Rerun affected groups, the retained-source regressions, and the full suite. Convergence, independent quality review, E2E coverage decision and final verification remain mandatory.

## Evidence and limits

Read-only comparison found the prompt/schema resource and affected historical test/source files unchanged from pre-story commit bff540980800eb67254170a0c82e38678b63576d. This proves source provenance, not a historical runtime pass.

Schema current CRLF SHA256 is757985c26bb465d77cf28d6d916557e86c69e7a436d81437aaa56ba2229a5f4b; canonical Git/LF SHA256 is87c8fc21f261779a0d130f09eccf574363b56e6d7138ea9d8933172e6cdaac9a, matching its accepted projector literal. Canonical Git prompt hashes are ad32930ab6e0c0b4e05e4e8c1f3e4298082b2a648198015d31687b53ac8ac37f(v1) and010247bbb9d8c07c2bd60a17353e5a088f7b7ea5ab490647712027d762b30937(v2); their old expected literals do not match even after LF normalization.

The new SourceSnapshots migration legitimately changes the current migration inventory. This proposal does not delete it or revive historical feature plans. Tests that regenerate historical qualification reports have their new output preserved with this run and their tracked historical originals restored.

## Why approval is needed

The user limited work to Story13.1. The run-bmad-sprint-loop skill states: "Never expand scope to an unmet prerequisite without user authorization." These additional baseline files are outside the previously approved four-test reconciliation. Approval covers only the bounded maintenance above, not product changes, external qualification, another story, or a waiver. Declining leaves Story13.1 incomplete at its required full-suite gate.
