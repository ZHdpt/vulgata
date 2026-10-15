# Vulgata

Vulgata turns source across Systems and Repositories into source-linked Code Logic
Documents and Business Logic Documents, with explicit Uncertainties and Business/IT
Chat. Use the [canonical glossary](docs/UBIQUITOUS_LANGUAGE.md), especially
**Scan Control**, **Worker Agent**, **Cross-Repository Resolution**, **Source Snapshot**
and exact Document Versions.

The generic design is accepted; implementation is incomplete. Accepted story
definitions and historical passing tests do not prove the replacement product flow works.

## Start with the right authority

- **Resume or assess reuse:** read [project state](docs/agents/project-state.md), then
  verify the assigned checkout and current files. Work in the assigned worktree;
  another task's title or old handoff is not authority to inspect/change its checkout.
- **Plan or change Scan, Knowledge or Chat:** read the architecture gateway, its
  AD-1–AD-32 spine and the affected PRD requirements. Authority follows subject
  and accepted disposition, not file age.
- **Deliver a story:** use the epics for its definition and sprint status for
  lifecycle. Verify current authority before changing scope or activating a feature.
- **Write production code, APIs, UI or migrations:** read
  [development conventions](docs/agents/development.md) and the
  [constitution](.specify/memory/constitution.md). Packages/build configuration are
  authoritative for installed versions; a planned dependency is not installed.
- **Author or run tests:** read [testing and verification](docs/agents/testing.md).
  Use owned resources and evidence appropriate to the claimed behavior.
  Default to `pwsh -NoProfile -File eng/test.ps1`; classify tests and helpers in the
  enforced suite inventory before adding external dependencies.
- **Explore domain decisions:** follow [domain guidance](docs/agents/domain.md).
- **Use GitHub issues:** follow [issue-tracker guidance](docs/agents/issue-tracker.md)
  and [triage labels](docs/agents/triage-labels.md); publishing requires task authority.

## Boundaries that apply throughout

- Core is domain-only. Infrastructure owns domain persistence/adapters. The existing
  Identity context/migrations remain in Web/Data; documentation adoption does not
  relocate them. Agent Execution uses injected contracts, not direct EF or arbitrary
  host access.
- Deterministic Scan Control owns scheduling/recovery; Knowledge owns accepted
  semantics, publication and read selection; Chat owns dialogue/turns. MAF runs bounded
  conversations/tools. Configured providers do not imply automatic failover.
- Exact retained source and accepted Knowledge are separate from disposable tool
  state. CodeGraph is not CodeGraphContext. Ordinary readers receive scoped relative
  access; only trusted adapters obtain contained physical workspaces.
- Document Versions are immutable; canonical CL/BL content is structured JSON with
  derived human-readable views. Changed accepted evidence may publish new versions or
  relationship selections without rescanning unchanged source. Preserve exact history.
- Enforce live user/purpose/mode and all-contributor authorization before disclosure.
  Business Mode is BL-only; IT source reads are exceptional and revision-bound. With
  self-hosted providers, source/Documents stay inside organization infrastructure.
  Secrets remain protected; DB tools remain read-only/non-production only.
- Product UI is Simplified Chinese, Fluent/Library Oak, Business/light and IT/dark.
  Keep Knowledge reads independent of management permissions. Developer conversation
  follows the user's requested language.
- Report static inspection, controlled tests and real runtime/provider evidence
  separately. Existing CLI wrappers, placeholders and simulated output are not proof
  of native MCP, generic Scans or genuine Chat streaming.
- Runtime startup applies migrations and seeds roles. Establish authorized database,
  provider, container and process boundaries before runtime work; legacy helpers are
  not permission to drop shared databases, stop unrelated processes or reuse secrets.
- Preserve unrelated edits and historical specs/evidence. Old Epics 4/11/12 and
  `docs/scanning_plan_july/` describe their own historical boundaries, not current
  execution instructions. No deadline/compile-only waiver carries into new work.

## Maintaining these instructions

Keep always-needed constraints here and conditional details in linked project-local
guides. After meaningful implementation changes, recheck the dated project
state and affected pointers; correct obsolete facts instead of appending turn logs.
Preserve a pitfall until its cause is removed. `CLAUDE.md` links to this file; Copilot
and Trae entry points should route here rather than duplicate its rules.

<!-- SPECKIT START -->
Story 13.1 is complete in specs/015-retained-source-access. Its plan, workflow state
and evidence record the completed boundary, not an active feature selection.
Story 13.2 remains backlog; select and activate the next authorized story deliberately
from the current tracker.
Existing specs/001 through specs/015 retain historical scope and approval boundaries.
<!-- SPECKIT END -->
