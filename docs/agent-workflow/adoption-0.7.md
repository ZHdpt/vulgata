# Governed-delivery 0.7 adoption

Updated: 2026-09-16. This is Vulgata's installation/resumption record, not product
verification or authorization to start a story.

## Installed boundary

- Distribution: **0.7.0**, copied from `governed-delivery-workflow` commit
  `e2ca94dcdd15832381b589bbcee76a4981bb490d`; that source project remained read-only.
- BMad: **6.12.0**, upgraded with the pinned official installer while retaining
  core/BMM, Codex/Copilot/Claude/Trae integrations, project paths and existing shims.
  See the [upstream release](https://github.com/bmad-code-org/BMAD-METHOD/releases/tag/v6.12.0).
- SpecKit: **1.0.6**, PowerShell scripts; existing installation/extensions retained.
- Installed skills: `run-governed-delivery`, `governed-tdd`, `close-governed-epic`,
  `audit-governed-architecture`. Their installed references own workflow rules;
  old documentation paths now forward instead of maintaining duplicate contracts.
- [Configuration](../../.governed-delivery/config.toml) selects `bmad-6.12`,
  `speckit-1.0.6`, and enabled `bmad-review-codebase-design` architecture review.
  The required BMad review and Matt Pocock `codebase-design` skills are present.
- Existing Codex topology already matches the generated fragment: four threads,
  one delegation level. Current profiles are installed together with validators.
  Do not dispatch the retired author/orchestrator roles.
- Build/test commands retain the Vulgata Web build and `Vulgata.Tests` test project.
  Their presence in configuration is not permission for unscoped runtime/DB work.

Local checkpoints:

1. `53f4afe22f424b2c55c97477a9968444a346d822` — BMad dependency upgrade.
2. `015ec2de8e89e2b3f51c25bf054483da1cb755fa` — new workflow/configuration and
   legacy installation retirement.
3. The subsequent `docs: align project guidance with governed delivery 0.7` commit
   records this handoff, authority pointers and project-memory updates.

No branch switch, history rewrite, push, product-code change or story activation
belongs to this installation.

## Preserved history and common selection

All 14 existing `specs/*/workflow-state.json` files, plans, evidence, activation
receipts, epic gate/retrospective records and sprint lifecycle were left unchanged.
Twelve states are complete, including Story 13.1. Story 11.5 remains historical
incomplete work; Story 12.5 remains terminated incomplete. Neither is a resume queue.
No active current-queue state needs 3.0-to-3.1 adoption; new activations use 3.1.

The ignored local `.specify/feature.json` selection of completed Story 13.1 was
cleared. No replacement feature was selected. Corrected AGENTS context states that
13.1 is complete and 13.2 remains backlog. Deliberately select the next authorized
feature; do not infer it from file modification times.

`paths.project_lessons` uses `docs/project-lessons.md`. No empty lessons file or
speculative retrospective was created. Root curates verified reusable lessons during
future bounded story lookback; existing development/testing guidance remains valid.

Removed installation assets: three old workflow skill trees, four superseded Codex
profiles, thirteen old Copilot workflow profiles, and the two old run/close prompts.
Their tracked versions remain recoverable in Git. Unrelated helpers, third-party
skills (including Matt Pocock TDD), and historical workflow reports were preserved.
The local Git safety hook was renamed, not removed; Copilot workflow parity is not
claimed by preserving that hook or unrelated helpers.

## Verification and unresolved compatibility

Observed installation checks:

- BMad tracker validation: valid, no problems or legacy status mappings.
- BMad sprint-planning tests: **37 passed**.
- Installed dependency check: passed, no warnings; all four official skill
  validations passed; **56 installed files** matched the release source byte-for-byte.
- Renamed Git guard: five non-executing command-payload checks passed.
- Release regression suite: **204 passed**, plus distribution/source-contract
  validation. Run from the distribution root with bytecode writes disabled;
  an initial invocation from Vulgata had import-path errors, corrected by using
  the suite's required working directory. All **85 tracked source files** retained
  their pre-installation hashes; source HEAD and clean status were unchanged.
- Product authorities, story artifacts and lifecycle match the pre-upgrade baseline
  `8d937054025bf8865dff537c59ec253d5048c6b8`.

The aggregate workflow validator is **not green**: it reports **258 findings**, all
under preserved historical story records, with no installation/profile/schema or
configuration-contract errors. These include old completion-commit schemas/pending
snapshots, absent historical configuration, unresolved checkpoint IDs/trailers,
plan hashes/classification and old counters. The old validator did not perform all
of these checks. Installation does not repair or erase their evidence.

In particular, Story 13.1 has one targeted failure: its completion commit
`6f312858264ecba8088c87c6b09a00813a652091` predates
`.governed-delivery/config.toml`. Adding configuration now cannot change that commit.
**Strict Epic 13 closure remains blocked on this historical binding.** A separately
reviewed legacy-checkpoint compatibility fix is needed in the distribution; do not
waive the gate, synthesize approval, rewrite completed states/history, or claim
epic-close readiness. The distribution was not modified during this adoption.

No application build/full product test suite, live agent forward-test, paid model
probe, provider run or database/runtime operation was performed for this installation.
Prior Story 13.1 product results remain their own historical evidence.

## Next authorized run

Start a fresh Codex session in the intended Vulgata checkout so the installed skills
and profiles are discovered. For orientation without activation:

```text
Use $run-governed-delivery dry-run for next.
```

Recheck current queue, ownership and dependencies. New story execution uses the
installed targeted guard against that story's state, not a bulk history rewrite:

```powershell
python .agents/skills/run-governed-delivery/scripts/check_dependencies.py --target .
python .agents/skills/run-governed-delivery/scripts/validate_workflow.py --repo . --story-state specs/<selected-feature>/workflow-state.json --next-stage <stage> --json
```

Preserve the existing planned-change and implementation approval gates. Optional
drafting input is not consent, and installation approval does not activate Story 13.2.
