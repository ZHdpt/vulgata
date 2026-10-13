---
name: Vulgata architecture gateway
type: architecture
status: final
updated: 2026-09-11
adoption_approved: 2026-09-11
source_baseline: 2bb83c3b44fc0afc07ae253ec1df5fab4399b964
---

# Vulgata architecture

## Authority and adoption status

The user approved the [September 11 generic-product planning correction](sprint-change-proposal-2026-09-11-generic-product-reconciliation.md). This gateway and the [AD-1–AD-32 spine](architecture/ARCHITECTURE-SPINE.md) are the finalized current target architecture after independent adoption review. They replace the obsolete active June/July/August architecture routing; they do not authorize implementation or resume a historical release.

The target was externally finalized on September 10 at source baseline `2bb83c3b44fc0afc07ae253ec1df5fab4399b964`. The original spine's SHA-256 is `CC7E5AC92810734549607F38399720A027E3E237320ACEC30926FBF24D6FE093`. The repository copy preserves all normative AD headings and Binds/Prevents/Rule text, with only metadata/provenance/adoption framing changed. External acceptance and repository adoption are separate from code completion and runtime qualification.

| Subject | Current authority | Boundary |
| --- | --- | --- |
| Product outcomes, priorities, required/deferred/original-backlog scope and qualification obligations | [Canonical PRD](prds/prd-vulgata-2026-06-12/prd.md) | Product WHAT; stable FR/NFR dispositions remain explicit. |
| Domain terms | [Ubiquitous Language](../../UBIQUITOUS_LANGUAGE.md) | One canonical glossary; historical terms do not silently acquire new semantics. |
| Platform/project conventions and authority navigation | This gateway | Preserved foundations and the scoped status of older records. |
| Generic Scan, Knowledge and Chat design | [Architecture spine](architecture/ARCHITECTURE-SPINE.md) | Module ownership, source/tool binding, accepted evidence, publication, read contexts, execution and deployment invariants. |
| Delivery decomposition and queue | [Epics](../epics.md), [sprint status](../implementation-artifacts/sprint-status.yaml), then a deliberately activated feature | Planning is not permission to bypass story gates or start an old feature. |
| As-built behavior and demonstrated results | Source code and explicitly scoped implementation/review/qualification records | Evidence describes its recorded boundary; old passing tests do not qualify generic replacements. |
| Agent working rules | [AGENTS.md](../../../AGENTS.md) and [agent guidance](../../../docs/agents/) | Coding, security, and testing conventions continue to apply. |

Authority is assigned by subject and explicit accepted disposition, not by document date. Archived material and superseded companions are historical evidence, never current fallback instructions. Any conflict with an existing ADR must be surfaced rather than silently overridden.

## Product scope and preserved foundations

Vulgata extracts and explains business logic across Systems and Repositories through source-linked Code Logic Documents and Business Logic Documents, Cross-Repository Resolution and wiki-first Business Mode/IT Mode Chat. The PRD governs the complete obligation set; the spine is not permission to erase requirements it does not design.

Preserve identity/roles, assigned-System management, Repository administration including standalone Repositories, provider configuration/overrides, encrypted secrets, database-connection administration and advisory Global/System/Repository context. Existing implementation assets remain useful, but the generic end-to-end Scan/Knowledge/Chat workflow is not yet implemented or newly qualified by this adoption.

The current target includes ordinary standalone scans, real Scan progress, visible Uncertainties, version-aware reading/history/citations, a basic manually refreshed scoped graph and genuine Chat streaming/reattachment. Product HITL, automatic/incremental update scanning, outward wiki MCP, Chat attachments, Worker-triggered partial library scans, new external operational-information tools, dependency-aware/multiple-LSP analysis and advanced/live portfolio graph remain explicitly deferred as bounded in the PRD/spine.

Prompt Workbench, dedicated unknown-library inference and general Repository/System/global MCP configuration/consumption retain their original-capability backlog dispositions. Native CodeGraph MCP and internal wiki tools are separate current obligations. A missing spine decision is not a feature cancellation or new deferral.

## Platform and project boundaries

Retain the .NET 10/Blazor Interactive Server modular monolith, PostgreSQL 17 via EF Core/Npgsql, Fluent UI and CommunityToolkit.Mvvm. Package files and project targets own the installed version seed; the spine's version table records the observed baseline, not an upgrade request. No additional project, service, package or framework is introduced by this adoption.

`Vulgata.slnx` contains the six .NET projects under `src/dotnet/`:

| Project | Preserved boundary |
| --- | --- |
| `Vulgata.Core` | Domain entities/rules and service/repository interfaces in `Entities/` and `DomainServices/`; no EF dependency or persistence-specific types exposed. |
| `Vulgata.Infrastructure` | Domain persistence, `Data/VulgataDbContext.cs`, `Data/Configurations/`, domain migrations, repositories and source/tool/storage adapters. |
| `Vulgata.Shared` | DTOs, input validators, `LoadState`, authorization policy and role names. |
| `Vulgata.Agents` | Agent execution, result contracts and externalized/embedded prompts; generic execution consumes injected contracts, not direct EF or arbitrary filesystem/process access. |
| `Vulgata.Web.ViewModels` | Testable MVVM ViewModels using the shared contracts and loading states. |
| `Vulgata.Web` | Composition in `Program.cs`, Razor `Components/`, minimal `Api/<area>/` endpoint groups, Identity glue and presentation. |

The existing Identity context and its migrations are physically in `Vulgata.Web/Data/`; domain context/migrations are in Infrastructure. This is a truthful existing-placement exception, not a relocation requirement. `ApplicationDbContext` and `VulgataDbContext` share PostgreSQL with separate `identity`/`vulgata` schemas and migration history; startup migrations and role seeding remain the baseline. No migration is run by documentation adoption.

Conceptual modules in the spine are responsibilities, not a new physical project tree. Scan Control owns durable orchestration; Knowledge owns accepted semantics and read/publication selection; Resolver/Flow Planner proposes; MAF performs model/tool conversations; Infrastructure implements the shared fenced acceptance/completion contracts; Web composes. Existing project references or historical implementation classes are not proof that these target boundaries are already realized.

The deployment target is exactly two Compose services: Vulgata with bundled local CodeGraph/JDT-LS tools, and PostgreSQL. Run the application and bundled tool processes as a non-root container user with only the required writable data/workspace mounts; this preserves the original runtime security baseline. Docker-first development, configured durable source/data/Data Protection keys, disposable Tool Workspaces, bounded resource controls and coherent operator backup/restore follow AD-15/30–32. No host-installation prerequisite, remote tool bridge, separate tool/LLM service, automatic provider failover, package upgrade or current Docker/restore-readiness claim is inferred. Initial local-source LSP restrictions disable automatic import/build/restore, not all network access.

## Implementation conventions

### Persistence, API and execution

- Keep nullable reference types, implicit usings, analyzers and centralized package versions. Async methods use `Async`; private fields use `_camelCase`; tables/columns use PascalCase with explicit existing mappings preserved.
- Use a specific repository per aggregate root. Interfaces live in Core; implementations and storage-specific query logic stay behind Infrastructure contracts. No generic `IRepository<T>`, `IQueryable` or `DbSet` leakage.
- Configure domain entities through assembly-scanned `IEntityTypeConfiguration<T>` classes in `Data/Configurations/`, without entity data annotations. Preserve domain invariants and immutable Document Versions/history; accepted-evidence reconciliation may create new publications without a source rescan.
- Use FluentValidation for input and the Blazor validator component. Group plural-noun endpoints under `/api/` through `MapVulgataApiEndpoints()`; return direct DTOs and ProblemDetails, without wrapping envelopes. Preserve proper API 401/403 responses and exclusion from cookie redirects/status-page rewrites.
- Keep long-running work off request/circuit lifetimes. Durable jobs, ownership fences and atomic acceptance own correctness; in-memory channels and post-commit notifications are replaceable wakeups, not durable workflow truth.
- Use UTC timestamps, established JSON conventions and sanitized structured `ILogger<T>` diagnostics. Preserve the PRD's append-only `log.md` operation/target/timestamp output obligation; observability does not silently replace its required format. Never log source, credentials or hidden reasoning.
- Externalize prompts as embedded resources under `Vulgata.Agents/Prompts/` with supported deployment overrides. Do not hardcode production prompts or treat retrieved source/wiki text as instructions.

Preserve structured JSON as canonical Code Logic/Business Logic content, backed by the existing `ContentJson`/PostgreSQL `jsonb` boundary. Human-readable Markdown and retrieval projections derive from that content; they are not competing editable stores. This concerns wiki Documents, not Chat's streamed prose or conversation records.

### Security and user experience

- Preserve ASP.NET Core Identity cookies, bcrypt password hashing, seeded Administrator/SystemOwner/User roles and scoped SystemOwner assignments. Apply management policies to management actions; reader/history/citation access is Knowledge-authorized, not management-only by page placement.
- Encrypt provider/database secrets with Data Protection. Preserve read-only/non-production database-tool restrictions even while new agent-facing external-information integration is deferred. Self-hosted-provider operation must not send source/generated Documents outside organization infrastructure.
- Enforce user/scope/mode authorization server-side throughout tools, results, history, graphs and citation targets, including live revocation and composite-prose contributors. Business Mode reads BL only; IT Mode permits CL and exceptional exact-source inspection under the spine.
- Keep hubs authorized, domain events/hub method names past tense and reconnect behavior truthful. Scan status recovery and same-turn Chat reattachment are distinct contracts; advanced graph deferral does not defer live Scan progress or real Chat deltas.
- Retain Simplified Chinese-only UI, Fluent UI, Library Oak colors/type, Business/light and IT/dark themes, chat-first navigation and the management shell. The [UX design](ux-designs/ux-vulgata-2026-06-22/DESIGN.md) and [experience specification](ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md) preserve the visual/accessibility floor while aligning their feature scope with the PRD.
- Every data-loading ViewModel exposes `LoadState`: `Idle|Loading|Loaded|Refreshing|Empty|NoResults|Error|Cancelling`. Distinguish missing data, no results, errors, partial coverage and stale knowledge rather than substituting invented content.

### Testing and evidence

Keep the single `tests/Vulgata.Tests/` xunit/`WebApplicationFactory<Program>` project and `{Method}_{Scenario}_{ExpectedResult}` naming. Preserve existing harness cautions in AGENTS: complete rendered hidden inputs for SSR forms, action/formname targeting, redirect-prefix checks, complete injected component dependencies and SQLite query limitations. SQLite harness results are not PostgreSQL/runtime equivalence proof.

Future delivery requires accepted feature contracts, analysis, honest TDD RED/GREEN, review, conditional E2E/integration checks and proportionate real qualification. No build, test, provider call, database operation or runtime validation is performed or claimed by this architecture adoption. The September 1 cleanup's compile-only waiver does not carry forward.

## Scan, Knowledge and Chat architecture

Read the [single adopted spine](architecture/ARCHITECTURE-SPINE.md) for all AD-1–AD-32 rules, shared record ownership, processing/state relationships, replacement map, verification obligations and explicit deferrals. This gateway does not maintain a second paraphrased rule set or reconstruct 32 redundant ADRs.

Before dependent builders split, a single owning feature must settle shared identity/selectors, lifecycle schemas, acceptance/publication/read-context contracts and runtime limits. These are technical design details under settled invariants, not permission to reopen accepted product choices or silently inherit incompatible historical schemas.

## Historical architecture and evidence

The [pre-adoption archive manifest](../history/pre-generic-adoption-2026-09-11/manifest.json) identifies exact pre-images and hashes, including the [former full architecture](../history/pre-generic-adoption-2026-09-11/files/docs/bmad/planning-artifacts/architecture.md). Earlier readiness/coverage statements remain preserved there, but have no generic implementation-readiness effect.

| Historical record | Retained meaning and current disposition |
| --- | --- |
| [July technical plan](architecture/scanning-v1-technical-plan.md) and [data contracts](architecture/scanning-v1-data-contracts.md) | Historical planning/schema contracts; no generic preallocation, fixed concurrency or no-refresh authority. |
| [August bounded design](architecture/august-30-bounded-scanning-design.md) | Dated Epic 12/fixed-pair release boundary; not generic eligibility, Chat or Docker acceptance. |
| [AnchorDB+ plan](architecture/document-graph-anchordb-plus-plan.md) and [decision log](architecture/document-graph-decision-log.md) | Historical candidates, rationale and decisions; not current graph/update/HITL/schema instructions. Prior log entries are unchanged. |
| Earlier correction proposals and [September 1 CGC decommission](sprint-change-proposal-2026-09-01-cgc-executable-decommission.md) | Preserve their explicitly dated scope and cleanup outcome; no CGC restoration or inherited exceptional validation policy. |
| `specs/001` through the existing `specs/014` directories, story/checkpoint/review/qualification records | Immutable execution history; no relabeling as new semantic-unit contracts or newly qualified generic behavior. |
| [Story 12.5 termination](../../../specs/014-grounded-llm-wiki-chat-release-evidence/evidence/handoffs/story-12-5-termination-blocked-01.md) and [August 31 Chat baseline](../implementation-artifacts/epic-12-post-termination-chat-integration-baseline-2026-08-31.md) | Story 12.5 remains terminated incomplete; useful later transport hardening neither resumes nor completes it. |
| External assessment, chronological decisions and final spine/reviews | Source evidence and provenance for adoption, not a competing current PRD or implementation authorization. Earlier unapproved alternatives are not revived. |

Epics 1–4 and Stories 11.1–11.4/12.1–12.4 retain their recorded completion history. Story 11.5 remains incomplete; Story 12.5 remains terminated incomplete. Old Epics 5–10 are superseded as active definitions, not marked done. No old ID is reused and no historical workflow state is rewritten to manufacture success. Current queue navigation is owned by the reconciled epic/sprint artifacts.

## Adoption and delivery gates

This approval is for the listed documentation/backlog correction only. Independent input, rubric, reality, adversarial and cross-document checks confirmed preservation and consistent target boundaries; narrow preservation fixes and editorial checks are recorded in the adoption review summary. Architecture finalization supplies no production-readiness claim.

After deliberate new feature activation, follow current governed story planning. Following independent plan acceptance and before task generation, explicit user approval is still required whenever a plan adds/materially changes an Agent tool or Scan strategy, or changes/weakens/conflicts with the PRD. Approval must identify the trigger, approach, effects, implementation intent and affected plan/PRD references; later analysis, TDD, review, E2E, security and verification gates remain mandatory.
