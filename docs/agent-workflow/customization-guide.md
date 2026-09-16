# Vulgata governed-delivery customization

The portable workflow owns its installed skills, profiles, adapters and validators.
Vulgata owns [AGENTS.md](../../AGENTS.md), the Constitution, product authorities,
build/test commands, lifecycle artifacts and local approval requirements. Configure
the integration through `.governed-delivery/config.toml`; do not fork copied
workflow contracts or edit upstream BMad, SpecKit or unrelated skills incidentally.

Use the installed [catalog](../../.agents/skills/run-governed-delivery/references/agent-catalog.md)
and [dispatch contract](../../.agents/skills/run-governed-delivery/references/dispatch-contract.md).
Preserve direct-child roles, disjoint writes, independent reviews and root-only
lifecycle/evidence/Git authority. Local policy may add constraints; it does not
silently remove approval gates, refresh retry allowance or combine authors/reviewers.

Keep consumer guidance short and link to installed references. Changes to portable
behavior belong in the distribution project and need a reviewed release; changes to
consumer policy need appropriate Vulgata authority. A different delivery harness
requires explicit compatibility work, not a renamed entry point.

After an authorized integration change, run:

```powershell
python .agents/skills/run-governed-delivery/scripts/check_dependencies.py --target .
python .agents/skills/run-governed-delivery/scripts/validate_workflow.py --repo .
git diff --check
```

Report historical compatibility findings separately, including pre-configuration
checkpoint failures, as explained in [adoption](adoption-0.7.md); do not rewrite
completed stories to make an aggregate check pass. Those findings are not waived
for affected future gates. Validate the changed contract at its source and verify
fresh-session discovery. Live agent forward-testing is not a prerequisite, and
installation checks are not product/runtime qualification.
