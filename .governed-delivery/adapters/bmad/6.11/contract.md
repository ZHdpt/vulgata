# BMad 6.11 lifecycle adapter

This adapter supplies story and epic identity, dependency ordering, lifecycle transitions, sprint status, corrective-work routing, and optional retrospective activation.

The workflow calls installed BMad skills by name and reads target-repository artifacts. It does not execute or distribute `_bmad` internals directly. A consumer may relocate BMad artifacts through `.governed-delivery/config.toml` only after the workflow validator and lifecycle adapter both support those paths.
