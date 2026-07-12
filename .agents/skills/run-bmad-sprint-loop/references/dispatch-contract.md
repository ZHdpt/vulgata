# Dispatch Contract

Every worker dispatch contains exactly these seven numbered slots:

1. `role_and_objective`: Name the role and one bounded objective.
2. `authoritative_inputs`: Provide repository-relative paths to raw authoritative inputs; do not replace sources with another agent's conclusions.
3. `workflow_boundary`: State the current stage, BMad story key, feature directory, source commit, and pinned diff boundary.
4. `constraints_and_skills`: Name applicable repository constraints and installed skills the worker must follow.
5. `write_boundary`: List exact permitted writes and prohibited adjacent work. Only the active root orchestrator may stage or commit.
6. `output_and_evidence`: Define the output schema, artifact destination, and required evidence.
7. `completion_and_failure`: Define done conditions and how to report conflicts, unavailable evidence, unsafe actions, or other failures without advancing state.

Judges and reviewers receive raw pinned artifacts, not author reasoning. A correction dispatch may additionally include the unchanged raw finding because correcting it is the bounded objective.

## Approval and escalation

A schema-valid independent `ACCEPT` is sufficient approval for its gate and advances without duplicate user confirmation. Human approval remains mandatory for blocking analysis remediation, unresolved intent or scope, three failed revisions, Strong architecture recommendations or ADR conflicts, divergent or destructive VCS recovery, inseparable worktree ownership, unavailable required infrastructure, unsafe external actions, and ambiguous E2E failures.
