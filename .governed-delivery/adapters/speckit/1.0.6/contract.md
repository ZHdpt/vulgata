# SpecKit 1.0.6 definition adapter

This adapter owns version-specific feature activation, script selection, setup-output fields, hook behavior, and the boundary between SpecKit definition and governed construction.

- `SPECIFY_FEATURE_DIRECTORY` and `.specify/feature.json` identify the feature directory; `SPECIFY_FEATURE` is only a label.
- `setup-plan` returns `FEATURE_DIR`, not the retired `SPECS_DIR` key.
- PowerShell, Bash, and Python script installations are supported according to `.specify/init-options.json`.
- An unreadable `extensions.yml` blocks the affected stage instead of being skipped.
- `speckit-analyze` remains read-only. Its optional remediation question is suppressed; findings return to the delivery orchestrator's existing correction policy.
- Governed delivery supplies the implementation provider; do not also invoke `speckit-implement`. Behavior-changing slices require accepted-task, RED, minimal-GREEN, and final-GREEN evidence. Test-only coverage requires accepted-task, passing-test, and coverage-reason evidence without fabricated RED. Nonbehavioral work requires accepted-task plus risk-appropriate direct-verification evidence; behavior-preserving refactors use before/after regression or characterization checks. Follow governed-tdd's verification routing rather than file type. The existing `non_executable_evidence` manifest key remains the direct-verification baseline, not a file-extension eligibility rule.

SpecKit workflow slots and per-step integration configuration remain optional. The governed loop dispatches definition skills directly and does not add a second workflow engine.

## Governed Convergence Override

Root verifies the current implementation prerequisite and returns known unfinished work to its owner before convergence. The analyst matches each gap to unfinished tasks by source obligation, observable behavior, and defect evidence. Reuse a covering task rather than appending a duplicate; a shared requirement ID alone does not merge distinct defects, and a genuine regression after completed work remains a traceable correction.

Preserve the raw SpecKit result and report a distinguishable governed disposition: `converged`, `tasks_appended`, `tasks_reused`, or `return_to_definition`. Mixed reused/new work is `tasks_appended`; reuse is not clean convergence. Definition/authority defects return to `spec|plan|tasks` before speculative appends. Supply prerequisite evidence, source references, unchanged findings, reused/appended IDs, and before/after task hashes. The analyst remains append-only in the Convergence section, preserving every prior byte/ID; description/dependency changes belong to the active definition author. These overrides do not modify upstream skills or pretend that 1.0.6 natively returns the extra outcomes. The root applies the shared recovery budget across all dispositions.
