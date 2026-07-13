# Sprint Loop Customization Guide

## Recreate the Repository-Local Workflow

Another coding agent can reproduce this workflow without copying installed skill bodies:

1. Copy the contracts in `docs/agent-workflow/` and adapt repository authority paths and verification commands while preserving the stage/gate semantics.
2. Create `.agents/skills/run-bmad-sprint-loop/`, `.agents/skills/close-bmad-epic/`, and the workflow-owned `.agents/skills/audit-epic-architecture/` adapter with valid metadata/interfaces.
3. Have the adapter load and apply the installed `improve-codebase-architecture` Explore/report process under a read-only epic-close boundary. Reference installed BMad, SpecKit, Superpowers, and Matt Pocock skills by name; do not vendor or rewrite them.
4. Create the 13 project agents from [the agent catalog](agent-catalog.md) as `.codex/agents/*.toml`, with `name`, `description`, `model`, `model_reasoning_effort`, `sandbox_mode`, and `developer_instructions`.
5. Configure direct-child topology in `.codex/config.toml` with `agents.max_depth = 1` and a bounded thread count.
6. Recreate the verdict/workflow JSON schemas, seven-slot dispatch contract, and deterministic validator from [artifact contracts](artifact-contracts.md).
7. Run fresh-context baseline pressure tests, forward tests with each skill, loophole-closure tests, agent/schema validation, and a no-feature-code dry run before real story use.

The target repository must replace Vulgata-specific paths with its own sources of sprint truth, Constitution, ADRs, context documentation, glossary, build, and tests. For Vulgata, retain canonical terms from `docs/UBIQUITOUS_LANGUAGE.md`, including **Cross-Repository Resolution**, **Code Unit**, **Code Logic Document**, and **Business Logic Document**.

## Replace Model IDs

Model IDs are policy inputs, not workflow semantics. Update the TOML catalog and its validation expectations together:

- Assign the strongest available reasoning model to planning, plan/spec judgment, artifact analysis, implementation, review, orchestration, and architecture audit.
- Assign an economical model to bounded context synthesis and E2E execution.
- Preserve high effort for the orchestrator, planning, judges, implementer, reviewers, and architecture auditor; medium for context/spec/tasks; low for E2E.
- Preserve read-only sandboxes for all judges/reviewers/analysts and production-code prohibition for E2E.

Record the substitution and rationale in repository documentation. A cheaper model must not be used as a reason to omit a required gate or weaken evidence.

## Change VCS Policy

The default policy makes the active root orchestrator the sole local committer. A repository may change checkpoint frequency, commit message style, or choose a host-native changeset mechanism, provided it retains:

- explicit ownership and exclusion of unrelated user changes;
- immutable accepted artifact and verified implementation boundaries;
- one independently reviewable story per changeset;
- checkpoint identity resolvable to immutable history;
- no checkpoint for rejected or failing work;
- no concurrent worker mutation of the index/history;
- no implicit remote mutation or history rewrite.

A policy that forbids agent commits can replace commits with explicit human-approved checkpoints, but resume must still validate immutable boundaries before trusting review evidence. Changing VCS policy requires updating artifact contracts, workflow validator, operator expectations, and pressure tests together.

## Preserve Required Gates

Customization may rename roles or adapt artifact paths but must preserve:

1. One BMad story to one SpecKit feature with two-way mapping.
2. The order `speckit-specify → speckit-plan → speckit-tasks → speckit-analyze → speckit-implement`.
3. Fresh independent spec and plan judgments.
4. Explicit user approval before any remediation from blocking read-only analysis findings, followed by dependent regeneration and analysis rerun.
5. Observed red-green-refactor evidence for every implementation group.
6. Independent acceptance and quality review, with Critical/High findings returned to fresh implementation.
7. Recorded conditional E2E decision and a test-only economical runner.
8. Full verification and valid BMad lifecycle transitions.
9. Epic architecture audit with disposition of Strong recommendations and ADR conflicts.
10. Sole-controller checkpointing and safe resume.

These are invariants, not optional defaults. If a target system lacks BMad or SpecKit, replace the owning artifact system with an equivalent contract rather than deleting the gate.

An independent judge/reviewer `ACCEPT` is sufficient approval for its stage. A customization must not add a duplicate human sign-off after accepted spec, plan, acceptance, or quality gates. It must retain explicit human direction for blocking analysis remediation; unresolved intent/scope or three `REVISE` outcomes; Strong architecture recommendations and ADR conflicts; divergent/destructive VCS recovery; inseparable ownership; unavailable required infrastructure; unsafe external actions; and ambiguous E2E failures. `BLOCKED` always stops immediately and never consumes a revision attempt.

## Package a Later Plugin

After repository-local story and epic runs pass, a plugin may package the same capabilities:

1. Move all three workflow skills, their schemas/scripts/references, and the 13 agent templates into a plugin source directory.
2. Add `.codex-plugin/plugin.json` with valid metadata and enumerate packaged skill/agent resources according to the current Codex plugin format.
3. Parameterize repository-specific authority paths, model aliases, verification commands, and VCS checkpoint adapter.
4. Keep project-local overrides for Vulgata's BMad paths, Ubiquitous Language, ADR/context map, and .NET commands.
5. Install the plugin through the supported cachebuster/reinstall flow and rerun RED–GREEN–REFACTOR skill tests in a clean repository fixture.
6. Confirm the packaged version does not modify installed upstream BMad, SpecKit, Superpowers, or Matt Pocock sources.

Packaging is distribution work, not authorization to push, merge, rebase, open a pull request, or mutate a remote.
