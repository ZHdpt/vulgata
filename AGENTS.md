# Vulgata

Vulgata turns source across Systems and Repositories into source-linked Code Logic
Documents and Business Logic Documents, with explicit Uncertainties and Business/IT
Chat. Use the [canonical glossary](docs/UBIQUITOUS_LANGUAGE.md), especially
**Scan Control**, **Worker Agent**, **Cross-Repository Resolution**, **Source Snapshot**
and exact Document Versions.

The generic design is accepted; implementation is incomplete. Accepted story
definitions and historical passing tests do not prove the replacement workflow works.

## Start with the right authority

- **Resume or assess reuse:** read [project state](docs/agents/project-state.md), then
  verify the assigned checkout and current files. Work in the assigned worktree;
  another task's title or old handoff is not authority to inspect/change its checkout.
- **Plan or change Scan, Knowledge or Chat:** read the
  [architecture gateway](docs/bmad/planning-artifacts/architecture.md), its
  [AD-1–AD-32 spine](docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md)
  and the affected [PRD](docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md)
  requirements. Authority follows subject and accepted disposition, not file age.
- **Deliver a story:** use [epics](docs/bmad/epics.md) for its definition and
  [sprint status](docs/bmad/implementation-artifacts/sprint-status.yaml) for lifecycle.
  Follow the installed [`run-governed-delivery`](.agents/skills/run-governed-delivery/SKILL.md) skill and
  [workflow adoption](docs/agent-workflow/adoption-0.7.md); preserve its current
  model/escalation strategy and single-owner gates. Deliberately activate one feature.
- **Write production code, APIs, UI or migrations:** read
  [development conventions](docs/agents/development.md) and the
  [constitution](.specify/memory/constitution.md). Packages/build configuration are
  authoritative for installed versions; a planned dependency is not installed.
- **Author or run tests:** read [testing and verification](docs/agents/testing.md).
  Use owned resources and evidence appropriate to the claimed behavior.
- **Explore domain decisions:** follow [domain guidance](docs/agents/domain.md).
  **Edit workflow customizations:** follow
  [the customization rules](.github/instructions/governed-delivery-customizations.instructions.md).
  Governed delivery uses the installed Codex profiles; no Copilot workflow parity is claimed.
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

## Planned-change authorization

After an independent `plan-review` accepts a story plan and before task generation,
the orchestrator MUST pause for explicit user approval (or applicable approval by
the user's specifically designated delegate under "Delegated approval" below) when
the plan does any of the following:

- adds, exposes, or materially changes a **Worker Agent** tool or other agent tool;
- adds or materially changes a **Scan** strategy, including parsing, filtering,
  scheduling, grouping, retry, or dispatch behavior; or
- changes, weakens, or conflicts with a requirement in the current PRD.

The approval request MUST identify the trigger, explain the proposed approach and
its effects, state the orchestrator's implementation intent, and cite the affected
plan and PRD material. It is approval to proceed from the accepted plan to task
generation; it does not waive any later analysis, TDD, review, E2E, security, or
verification gate. If none of these triggers applies, the ordinary workflow
advances without this additional approval.

## Delegated approval

Human approval remains the default. For a specific story, the user may explicitly
designate a stable Codex task/chat ID to authorize only planned-change authorization
after accepted independent plan review and implementation approval after accepted
independent definition review. The designation must state the story scope, conditions,
and these gates, and must be recorded with its source in the story's existing decision
evidence. The story root verifies the actual user instruction, delegate response,
current accepted review and artifact hashes, and continued scope before proceeding;
the delegate returns its decision but never writes approval files. A task title alone
does not identify a delegate; no delegate may substitute another task or subdelegate.

This designation does not cover recovery extensions, authority/blocker resolution,
prior-authority conflicts, architecture changes outside the accepted plan, epic
dispositions, retrospective choices, or separate access/spend/secret/production
permissions. Existing gate order, recovery limits, worker ownership, and verification
remain in force. If provenance, scope, or conditions are unclear, require the user.

## Maintaining these instructions

Governed delivery 0.7.3 is installed with `.governed-delivery/config.toml` selecting
BMad 6.12 and SpecKit 1.0.6. New stories use state 3.1; historical states/evidence stay
unchanged. One retained definition author owns spec/plan/tasks and corrections;
the independent reviewer retains review history. Coherent implementation groups
retain separate test and production owners. Root owns bounded recovery, selective
lookback, lifecycle/evidence/approvals and Git. Read the installed conditional
references instead of reconstructing rules from historical workflow documents.
Optional drafting questions are not approval; preserve every formal gate above.

Sol/Luna profiles now use GPT-6 at their existing reasoning efforts; Astra recovery
is unchanged. Route verification by accepted behavior and risk: RED/GREEN for
changed production behavior, truthful coverage/refactor checks or direct verification
otherwise. Do not invent tests or artifact-integrity requirements for ceremony;
dispatch only needed owners. Existing consumer policies and approval gates still apply.

Keep always-needed constraints here and conditional details in linked project-local
guides. After meaningful implementation or workflow changes, recheck the dated project
state and affected pointers; correct obsolete facts instead of appending turn logs.
Preserve a pitfall until its cause is removed. `CLAUDE.md` links to this file; Copilot
and Trae entry points should route here rather than duplicate its rules.

<!-- SPECKIT START -->
Story 13.1 is complete in specs/015-retained-source-access. Its plan, workflow state
and evidence record the completed boundary, not an active feature selection.
No story is activated by workflow installation. Story 13.2 remains backlog;
select and activate the next authorized story deliberately from the current tracker.
Existing specs/001 through specs/015 retain historical scope and approval boundaries.
<!-- SPECKIT END -->
