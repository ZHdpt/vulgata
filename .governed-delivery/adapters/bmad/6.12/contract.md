# BMad 6.12 lifecycle adapter

This adapter preserves the 6.11 story/epic status vocabulary and deterministic sprint-planning lifecycle while accepting BMad 6.12 installations.

Version detection reads exactly `installation.version` from `_bmad/_config/manifest.yaml`; module-level version fields never satisfy the adapter.

BMad Build remains outside governed delivery because SpecKit definition, governed TDD, convergence, and quality review already own that work. BMad 6.12 review integration must preserve a verdict and evidence for every triaged finding and use `diff_file` rather than the retired `diff_output` customization variable.

The workflow reads repository authorities directly. It does not rely on BMad `persistent_facts`, deprecated shims, or checkpoint-preview aliases.
