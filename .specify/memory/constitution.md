<!--
Sync Impact Report
Version change: 1.0.0 -> 2.0.0
Bump: MAJOR. Align previously incompatible project/data ownership and Document
versioning rules with the user-approved September 11 generic-product architecture.
This local amendment is not a claim of completed PR/Administrator review or merge.

Modified principles:
- I. Layered Project Boundaries: truthful Web/Data Identity exception; one owning
  module per contract; remove the inaccurate one-direction project-reference diagram.
- II. Domain Purity & Ubiquitous Language: immutable versions also follow changed
  accepted evidence/flow dependencies; canonical JSON and historical provenance.
- III. Validation & API Contract Discipline: preserve validation, API and Chinese UI
  rules; replace unavailable named-memory references with project-local guidance.
- IV. Test-First Integration Harness: align behavioral TDD and nonbehavioral evidence
  with the current unified workflow; no fabricated RED or runtime claims.
- V. Security & Data Stewardship: live contributor/purpose/mode/source authority and
  provider boundaries; retain protected secrets and read-only/non-production DB tools.

Additional Constraints and Development Workflow updated to the adopted target:
deterministic Scan Control, bounded MAF execution, no implicit provider failover,
two-service Docker target and genuine Chat streaming/manual graph distinction.
No principles or maintainer amendment/review requirements removed.
Resolved constitution-template successfully; dependent templates/commands read this
file at runtime and were not modified by this constitution update.
Ratified date preserved. No unresolved template placeholders or deferred implementation.
-->

# Vulgata Constitution

This is development governance for the adopted generic product, not a statement that
the current code implements or qualifies that target. Product scope and dispositions
come from the current PRD; terminology from the canonical glossary; design from the
architecture gateway/spine. The working tree may still contain explicitly scoped
legacy implementations while replacement stories are delivered.

## Core Principles

### I. Layered Project Boundaries

Each project MUST retain its recorded responsibility. Domain and agent execution
code MUST NOT acquire database, arbitrary filesystem or process authority merely
because an infrastructure implementation is available.

- **Vulgata.Core** owns domain entities/rules and service/repository contracts. It
  MUST NOT depend on EF Core, Infrastructure or Web.
- **Vulgata.Infrastructure** owns domain persistence, `VulgataDbContext`, domain
  migrations/configurations and source/tool/storage adapters.
- **Identity placement exception:** the existing `ApplicationDbContext`, Identity
  migrations and seeding remain in `Vulgata.Web/Data`. Web startup composes and
  migrates both contexts. Planning adoption does not relocate these files or prove
  that only Infrastructure currently touches EF/database APIs.
- **Vulgata.Shared** owns DTOs, validation and common presentation/access constants;
  it MUST NOT depend on Infrastructure or Web.
- **Vulgata.Agents** executes bounded conversations through injected domain/tool
  contracts and externalized prompts, without direct EF or arbitrary host access.
- **Vulgata.Web** owns composition, API/presentation and the recorded Identity
  exception; ViewModels retain their testable presentation responsibility.

Scan Control MUST own scheduling/recovery; Knowledge MUST own semantic acceptance,
publication and read selection; Chat MUST own dialogue/turns. Computational modules
propose, and Infrastructure implements the owners' atomic/fenced contracts. New
records and abstractions MUST be introduced when an owning story needs them, not as
an upfront schema for every future feature.

Rationale: preserve testable domain boundaries and one policy owner while stating
the existing layout accurately.

### II. Domain Purity & Ubiquitous Language

- Domain entities MUST have no data annotations. Domain EF mappings live in
  Infrastructure's `IEntityTypeConfiguration<T>` configurations.
- Each aggregate root MUST use a specific repository contract; no generic
  `IRepository<T>`. Domain persistence MUST NOT expose `IQueryable` or `DbSet`
  through its public repository boundary. The existing Identity context is the
  explicit project-placement exception, not permission to leak domain storage.
- Accepted Code Logic and Business Logic content MUST use immutable Document
  Versions and canonical structured JSON. Markdown/reading projections are derived,
  not competing editable authorities.
- New versions MAY result from accepted source analysis or changed accepted
  relationship/evidence/flow dependencies. Relationship selection may advance
  without rewriting unchanged Code Logic. Exact historical dependencies and Version
  Chains MUST survive; previous Document Versions are never deleted or retargeted.
- Drafts, incomplete file results and unqualified legacy representations MUST NOT
  be relabelled as newly accepted generic Knowledge.
- Use [the canonical glossary](../../docs/UBIQUITOUS_LANGUAGE.md) verbatim. Surface
  missing or conflicting domain concepts rather than inventing a competing glossary.

Rationale: versioned Knowledge can improve after later evidence without requiring
unchanged-source rescans or losing historical meaning.

### III. Validation & API Contract Discipline

- Use FluentValidation for domain/request input and the established Blazor form
  integration. Do not substitute entity data annotations for domain validation.
- Minimal APIs MUST use the recorded plural-resource grouping and ProblemDetails
  convention. API failures MUST retain correct 401/403 behavior rather than cookie
  redirects or HTML status-page rewrites.
- Product UI MUST remain Simplified Chinese for V1, with the approved Fluent/Library
  Oak patterns and accessibility floor. This does not dictate the language of
  developer conversation.
- Async methods use the `Async` suffix, private fields use `_camelCase`, and
  persistent tables/columns use PascalCase. Domain events and hub methods use past
  tense.

Runtime/source details and verified commands belong in
[development guidance](../../docs/agents/development.md); test-harness caveats belong
in [testing guidance](../../docs/agents/testing.md).

### IV. Test-First Integration Harness

- Behavioral implementation MUST follow the selected repository workflow's honest
  RED, minimal GREEN and surrounding-regression evidence. Tests and production
  changes retain the workflow's separate ownership.
- Documentation, configuration metadata and other nonbehavioral work MUST use
  proportionate truthful checks; never fabricate a failing test to satisfy a stage.
- Retain the single `tests/Vulgata.Tests` project and established integration
  harness. New behavior SHOULD cover its happy path and applicable authorization
  boundary; a missing applicable boundary test requires explicit review rationale.
- Required build/tests and risk checks MUST pass at the governed delivery boundary.
  Preserve the actual analyzer configuration/severity; do not call warnings errors
  without configuration or command evidence.
- Fixtures, SQLite, schema validity and old passing tests MUST NOT substitute for
  required PostgreSQL, real tool/provider, lifecycle or usefulness qualification.
  Explicitly distinguish static, controlled-test and real-runtime evidence.

Rationale: the existing harness has useful mechanics and specific pitfalls, but
accepted evidence must match the behavior and environment being claimed.

### V. Security & Data Stewardship

- Retain bcrypt password hashing, seeded roles and protected provider/database
  credentials through Data Protection. Required keys MUST remain usable across
  the declared replacement/restore boundary.
- Every disclosure and applicable acceptance/publication MUST enforce current
  user, purpose, source and all-contributor authority, including metadata/history.
  Business Mode is BL-only; IT source access is exceptional, bounded and tied to
  the recorded Profiled Revision. Mode choice grants no Repository permission.
- With self-hosted providers, source/Documents MUST stay inside organization
  infrastructure. External providers receive only necessary authorized task context.
  Agents MUST NOT choose credentials, arbitrary host paths or domain writes.
- Database tools MUST remain read-only/non-production only; deferred integrations
  create no permission to query or write production databases.
- Prompts MUST remain externalized. Source, wiki content and advisory context are
  data, not executable agent instructions. Diagnostics MUST exclude credentials,
  raw source, hidden reasoning and unnecessary sensitive payloads.
- Runtime verification MUST use explicitly scoped resources. Starting the Web host
  applies migrations/seeding; destructive cleanup or provider execution requires
  appropriate task authority and owned targets.

Rationale: protect proprietary source and generated Knowledge without confusing
configured capabilities with execution or disclosure permission.

## Additional Constraints

The [architecture gateway](../../docs/bmad/planning-artifacts/architecture.md) and
[AD-1–AD-32 spine](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md)
own the platform/project layout and generic Scan/Knowledge/Chat design. Package/build
files own installed versions/settings; a documentation target is not runtime proof.

Retain the .NET 10/Blazor modular monolith, PostgreSQL 17 and approved UI stack.
Deterministic Scan Control owns orchestration; Microsoft Agent Framework owns
bounded model/tool conversations. Configured providers do not imply failover.

The deployment target is Vulgata with bundled local CodeGraph/JDT-LS plus PostgreSQL
as two Compose services, with non-root application/tools, configured limits and
separate durable source/data/keys versus disposable tool state. Initial LSP policy
disables automatic import/build/dependency restore, not all networking. Genuine Scan
progress and Chat deltas differ from the basic manual-refresh graph.

Current required/deferred/historical/original-unmet scope is recorded in the
[PRD](../../docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md). Automatic
source-update scanning, product HITL and other deferred features MUST NOT silently
re-enter through old specs or examples. Native CodeGraph is not CodeGraphContext.

## Development Workflow & Quality Gates

For BMad story work, follow the installed `run-governed-delivery` skill and its
[workflow architecture](../../.agents/skills/run-governed-delivery/references/architecture.md) and
[state machine](../../.agents/skills/run-governed-delivery/references/state-machine.md). Resolve work from the
[current backlog](../../docs/bmad/epics.md) and
[tracker](../../docs/bmad/implementation-artifacts/sprint-status.yaml), then deliberately
activate its feature. Historical specs and file modification times are not selectors.

The root owns state/checkpoints and any authorized local commits. Preserve independent
definition/quality review, separate behavioral test/production ownership, convergence,
conditional E2E and pinned verification. A story-definition acceptance is not
implementation approval; earlier deadline/compile-only waivers do not carry forward.

After independent plan acceptance and before tasks, explicit user approval remains
required for new/materially changed Agent tools, Scan strategy or PRD requirements,
as detailed in [AGENTS.md](../../AGENTS.md). The current workflow also governs
architecture decisions and other human gates. Do not substitute a cached role catalog
or obsolete harness adapter for those authorities.

## Governance

This constitution defines development constraints and supersedes conflicting ad-hoc
practices. The PRD owns product dispositions, the glossary owns terms, and approved
architecture/ADRs own design. Conflicts MUST be surfaced rather than silently resolved
by date, convenience or an old spec. Historical accepted boundaries remain evidence.

**Amendment procedure:**

1. Propose the amendment in a PR editing this file.
2. Include old/new text, rationale and a migration plan for affected in-flight work.
3. Obtain review by at least one Administrator-role maintainer.
4. On merge, update the amendment date and semantic version: MAJOR for incompatible
   principle/governance redefinition, MINOR for material additions, PATCH for
   non-semantic clarification.

The version line is the single current version identifier. This working-tree update
does not assert a PR, maintainer review or merge has occurred. Its migration is
documentation/agent-authority alignment with the already approved generic design;
new feature plans must check this version, while historical feature records remain
unchanged.

Every PR MUST self-certify compliance with these five principles. Reviewers MAY block
a PR for a principle violation. An intentional exception requires recorded justification
and rejected simpler alternatives in the plan's Complexity Tracking section; it is not
an implicit waiver.
Day-to-day instructions are in [AGENTS.md](../../AGENTS.md).

**Version**: 2.0.0 | **Ratified**: 2026-07-07 | **Last Amended**: 2026-09-11
