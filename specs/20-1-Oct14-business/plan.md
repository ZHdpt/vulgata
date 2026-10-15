# Implementation Plan: OCT-BUSINESS

**Branch**: `oct14/business` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md)
**Input**: `specs/20-1-Oct14-business/spec.md`
**Status**: Planning artifacts complete; independent acceptance is recorded in plan-review.md. No implementation authorization.

## Summary

Deliver B0 real atomic whole-file CL acceptance, immutable explicit publication and shared live-authorized exact reads first. Then resolve a fixed accepted corpus with bounded source-supported evidence (B1), preserve distinct roots and synthesize useful document-first BL with atomic exact dependencies (B2). OCT-BUSINESS owns shared Knowledge semantics, persistence and all October migrations; integration owns shared Web composition. Other lanes consume one compiled contract checkpoint rather than inventing stores or DTO forks.

This is a new first-deliverable feature under the user's direct SpecKit instruction. Original governed stories are neither resumed nor marked complete. The October 6 delivery/integration/Business inputs supersede their broader task lists. Historical 18.5 `ed4f0205` supplies selected design ideas only.

## Technical Context

**Language/Version**: C# / .NET 10, existing nullable/implicit usings and analyzers.
**Primary Dependencies**: Existing EF Core/Npgsql, ASP.NET Core Identity, FluentValidation, installed provider/agent infrastructure and externalized prompts. No new package or service proposed.
**Storage**: PostgreSQL 17; existing OctoberDbContext, fixed `october` schema and `__OctoberMigrationsHistory`; canonical CL/BL JSON. Existing Identity/management data supplies live authorization facts.
**Testing**: Single xunit project and `eng/test.ps1`; SelfContained, PostgreSql, External and integrated E2E boundaries remain separate. Honest behavioral RED/GREEN with separate test/production ownership after authorization.
**Target Platform**: Existing Windows development worktree; deployment remains existing .NET/Docker target. Native tools are Scan-owned; this lane does not requalify them by proxy.
**Project Type**: Modular-monolith domain/contracts + persistence + bounded agent execution, consumed by generator and Web.
**Performance Goals**: Bounded pages and finite corpus/flow work, not an unmeasured throughput promise. Defaults and failure semantics in [contract](contracts/knowledge-contract.md); measure real store/provider waits and total duration during qualification.
**Constraints**: No source tools in Pass 2, no latest/legacy fallback, no new ACL platform, no global configuration changes, no Web October migration, no raw source/secrets in diagnostics. Provider/runtime work needs owned explicit targets.
**Scale/Scope**: One useful registration/login scenario crossing two prepared Repositories, at least one supported link and unknown; one/several/standalone Repository contracts remain valid. Broader source breadth follows only after the case works.

## Constitution Check

Pre-research and post-design assessment against constitution 2.0.0:

| Principle | Design result |
| --- | --- |
| I — Project boundaries | PASS: Core owns domain/ports, Shared transport validation, Infrastructure storage, Agents bounded proposal/synthesis, Web/Data Identity adapter; integration alone edits shared composition. |
| II — Domain purity/history | PASS: specific ports, no exposed EF queryables, immutable canonical versions, exact dependencies, no dummy ScanRun/Git origin or consumer stores. |
| III — Validation/API/UI | PASS: FluentValidation and normal naming; no new public endpoint in this lane. Consumers preserve ProblemDetails/401/403 and Chinese Fluent UI. |
| IV — Test-first harness | PASS at design level: separate future behavioral test/production ownership, meaningful authorized suites, independent review; no false runtime evidence. |
| V — Security/stewardship | PASS at design level: live Identity/contributor/mode/purpose checks, BL-only Business, protected providers and bounded resources, sanitized logs. |

The user explicitly selects direct SpecKit and excludes other skills. That instruction overrides the repository's story-runner routing for this new feature only; it does not waive integrity, review, TDD or runtime requirements. Root agent instructions and optional agent-context hooks are deliberately unchanged. October's approved fixed-batch scope defers generic durable refresh/restart/head machinery; it does not amend generic PRD obligations or claim them complete.

## Project Structure

### Documentation (this feature)

`spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/knowledge-contract.md`, `contracts/fixture-cases.md`, `checklists/requirements.md`, and an independent `plan-review.md`. No `tasks.md`.

### Source Code (future owned work, not created by planning)

| Owner | Concrete modules/files | Boundary |
| --- | --- | --- |
| Business | New `src/dotnet/Vulgata.Core/DomainServices/OctoberKnowledgeContracts.cs`, `OctoberKnowledgePolicy.cs`; new `Entities/OctoberKnowledge/` | Single acceptance/publication/read/identity/contributor/relationship/flow contract; no EF or host paths. |
| Business | New `src/dotnet/Vulgata.Shared/OctoberKnowledge/` DTOs and validators | Transport projections and validation, no competing identities or permission policy. |
| Business | Existing `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberDbContext.cs`, `Migrations/OctoberDbContextModelSnapshot.cs`, new additive migrations and `Knowledge/` adapters/configurations | Sole writer for Knowledge and serialized Chat persistence. Preserve setup rows/history. |
| Business | New `src/dotnet/Vulgata.Agents/OctoberBusiness/`, `Prompts/OctoberBusiness/` | Bounded accepted-document resolution and synthesis proposals; injected providers/ports, no source tools or database authority. |
| Business | New `src/dotnet/Vulgata.Web/Data/OctoberKnowledgeIdentityAdapter.cs` and feature-local registration module | Real live Identity bridge under recorded Web/Data exception. Integration owns activation; no Program.cs edit by Business. |
| Business | New `tests/Vulgata.Tests/October/Knowledge/` and shared labelled fixture there | Acceptance/read/authorization/relationship/flow tests. Suite-inventory additions coordinated through integration/test infrastructure owner. |
| Scan | `src/dotnet/Vulgata.OctoberGenerator/Program.cs`, `OctoberGeneratorModule.cs`, `PreparedSourceBinding.cs`, source/tool finalizer adapters | Admission, trusted completion/cancellation, actual provider/source caller and run command. Business supplies writer/finalization ports; does not implement Scan scheduling/tools. |
| Chat | Chat contracts/agent/tool/event/UI modules and per-turn read ledger | Chat supplies dialogue requirements; Business alone implements October mappings/migrations. |
| Explore | Reader route/components/ViewModels and bounded graph projections | One exact-reader destination for graph and Chat references; no direct EF or private wiki copy. |
| Integration | `src/dotnet/Vulgata.Web/Program.cs`, shared DI/API registration/navigation/routes, `Directory.Packages.props`, shared project/package files, shared test inventory coordination | Activates wiki-read module with frozen DefaultConnection + explicit batch/publication; no October migration or generator credentials at Web startup. |

Existing legacy KnowledgeStore and normal VulgataDbContext/Identity migrations are preserved. New October mappings follow existing project conventions with explicit configuration classes. No new project/context or ownership of other lanes' shared files is implied.

## Design and contracts

[Research](research.md) records alternatives and inspected source; [data model](data-model.md) specifies fields, constraints and transactions; [shared contract](contracts/knowledge-contract.md) settles interfaces, authority, bounds and failure semantics.

1. Separate proof of completed analysis from current write permission. The exact completed proposal can become ineligible after seal issuance. Scan and Business share a process-local per-attempt commit guard: fence/deadline/hard-stop and fresh authority checks serialize against commit submission. A seal followed by drain expiry cannot begin a transaction; expiry during staging rejects submission. Already-submitted commits settle by exact lookup, never cancellation-as-proof-of-rollback. The [F1 outcome table](contracts/knowledge-contract.md#f1--canonical-stopacceptance-ordering-and-scan-adoption) owns these meanings. Identical replay resolves durable handles; conflicting replay fails; unknown acknowledgement never triggers generation.
2. Use batch-scoped semantic identities and validated prepared-source anchors. CL owns structured JSON per discovered Code Unit, with material section/statement-group source/advisory/hypothesis qualifications preserved into BL and every consumer projection. Zero-unit acceptance is valid only with truthful completed coverage; failed drafts are invisible.
3. Publish explicit immutable membership. F5: a settled CL-only selection accepts empty relationship/flow/BL sets and serves Scan S2/B0 without B1/B2 or a Business provider call; Scan owns the early Generate path. B0 and B2 selections have different IDs; Web/consumers must explicitly select one. BL versions pin all actual dependencies and contributors. No mutable latest head or cross-batch lineage.
4. Keep ordinary read selection separate from operator setup. The selected coarse role policy permits live eligible Administrator/SystemOwner/User accounts, within explicit scope/publication and all live contributor bindings. It does not reinterpret management ownership as reading permission. A future request for per-user Repository ACLs is a policy/scope stop.
5. Bound lexical browse/search, exact sections/chunks and one-hop accepted relationships. Protected continuation tokens grant no permission. Business filtering includes metadata and dependencies; recheck live authority at each disclosure. No totals/continuations based solely on hidden items.
6. On natural finish or graceful operator stop, Scan closes dispatch, settles active/ambiguous writes and records accepted/zero-unit/failed/cancelled/unprocessed dispositions for every selected input. A settled stop permits partial-corpus finalization; paused/unsettled/indeterminate work does not. Freeze finite accepted evidence. Domain validation, not model confidence, accepts cross-Repository relationships after a concrete positive evidence checklist. Preserve supporting/opposing evidence and unknowns. Distinct roots and finite directed slices drive document-first BL; publication cannot recursively initiate inquiry/Scan.
7. Every matching/synthesis provider attempt acquires Scan's host-supplied execution/admission/accounting scope: one configured cap, host provider request/rate admission, remaining budgets, deadline, hard-stop cancellation and exactly-once actual/unknown usage settlement. No outer orchestration permit is held while child inquiries wait, so cap=1 cannot deadlock; no Business pool or cross-process Web permit service is introduced. See the contract F2 section.
8. Serialize Chat schema contribution under Business while Chat owns behavior. The initial Knowledge migration need not wait for dialogue fields; exact Chat contract agreement is prerequisite only for its additive migration.

## Small usable increments and prerequisites

These are delivery slices, not generated tasks. Dates are targets from the delivery plan, not estimates or authorization.

| Increment | Usable result | Prerequisites | Verification / exit |
| --- | --- | --- | --- |
| CP-K1 — contract, first after authorization | One compiling shared Core/Shared API and labelled fixture for all consumers | Accepted plan; user authority to proceed; Scan completion/terminal inventory and Chat/Explore shared read/handle semantics acknowledged; dialogue-specific agreement is deferred to its separate additive migration | An initial owner-published contract/fixture commit enables minimal consumer compile/conformance probes before dependent production is enabled; completed consumer implementations are not prerequisites. Probes cover F1 races, F2 cap=1/hard stop, F5 CL-only publication, identity and disclosure. Integration imports/records the hash. No real generation claim. |
| B0a — atomic CL store | Labelled full-file proposal accepted in real October store; exact replay/conflict/indeterminate lookup | CP-K1; trusted finalizer adapter; owned database and live producer authority | Controlled atomicity/anchor/zero-unit/cancel cases; owned PostgreSQL concurrent identical/conflicting operations and commit-ack loss. No public visibility before publication. |
| B0b — published CL reads (October 7 target) | Explicit CL publication usable by ordinary IT reader | B0a; empty relationship/flow/BL sets accepted, no B1/B2 or Business provider dependency; live ordinary-reader adapter; integration opt-in Web composition; Explore reader; Scan one-real-file route for real proof | Exact section/page, wrong database/batch, Business exclusion, all-contributor revocation, fresh ordinary User and safe outage checks. One real file -> PostgreSQL -> exact CL opened in reader. |
| B1 — supported relationship (October 7–8 target) | Fixed accepted corpus yields directed source-backed link plus honest unknown | B0; Scan terminal admission inventory and compatible host execution/admission/accounting scope; at least relevant accepted evidence | Finite candidate/cycle/ambiguous tests; no confidence-only acceptance; real provider/source reviewer verifies the link and missing fact. Query through shared related port. |
| B2 — rooted BL (October 8 target) | Distinct useful BL published with exact content/relationship/dependency selection | B1 outcomes complete; source-backed roots; configured generator provider plus the same host execution scope; all-contributor authority | Shared helper/two-root/local-only/cycle cases; failed atomic promotion invisible; actual CL inputs tracked; source reviewer checks conditions, branches, effects and gaps. Business reads BL only. |
| Dialogue addition — parallel after agreed contract | Chat stores ordered turns and idempotent outcomes with exact references | Chat supplies bounded contract/read ledger; Business owns additive migration | Atomic outcome+references, duplicate/conflict/lookup and one-active-turn constraints; Chat owns actual streaming/history qualification. |
| Integrated handoff (October 9–10) | Same real dataset consumed by Scan, Chat, reader and graph | Integration imports lane commits and controls dataset/schema window; actual provider/source/runtime approvals | One final combined build/SelfContained run after corrections, affected PostgreSQL/provider/UI checks, real references resolve same versions, denied contributor leaks nothing. Integration alone declares combined status. |

If the one-real-file CL path fails, fix its concrete seam before widening input breadth. If the cross-Repository case fails, narrow the input slice without replacing missing evidence with fabricated Knowledge or dropping required CodeGraph from Scan.

## Dependencies and contract checkpoint

- **Scan -> Business:** authenticated selected setup snapshot, verified prepared source metadata, non-model trusted admission/completion capability, root inventory with explicit terminal outcomes, fixed corpus finalization call, shared commit guard/fence semantics and real provider adapter with mandatory execution/admission/accounting scope. Scan consumer acknowledgement of corrected F1/F2 and the F5 early path is pending; no concurrence is inferred. Current Prepare/Select sessions are not ready-to-run analysis contexts.
- **Business -> all:** exact identity/operation/publication/read DTOs and semantics; common fixture with CL, BL, relationship, unknown and disallowed contributor; one owning implementation. CP-K1 starts with the initial contract/fixture commit and minimal conformance probes; it does not wait for completed consumers. These Markdown documents do not satisfy it.
- **Chat -> Business:** dialogue state/reference/size requirements. Business owns sole schema changes; missing dialogue agreement does not authorize Chat EF tables or block unrelated B0 work.
- **Explore -> consumers:** one exact-reader destination. Graph projects accepted directed edges; references remain handles until the route is supplied.
- **Integration -> all:** shared composition and package changes, frozen Web DefaultConnection, explicit selected publication, schema deployment window, final real dataset and coordinated qualification. Ordinary administration works without wiki; mismatched wiki does not fall back.
- **Runtime owner/user:** explicit owned targets, real source/provider authorization, live Identity/management records and protected key/log locations. No planning command starts hosts or databases.

## Verification and requirement traceability

| Spec requirements | Planned proof |
| --- | --- |
| FR-001–004; SC-001 | Completion proof versus current eligibility; seal -> drain expiry -> transaction denial; fence during staging versus commit already submitted; exact ambiguous lookup; CL-only publication without B1/B2; private completion forgery/cancellation races, full-file atomicity, zero units, concurrent replay/conflict, indeterminate lookup, unpublished exclusion, exact selection tests. |
| FR-005–007; SC-002/005 | Ordinary non-operator reads; deleted/moved/out-of-scope/advisory contributor, live role/stamp/lockout and outage probes; Business title/count/path/related rejection; stable bounded pages/chunks. |
| FR-008–009; SC-003/004 | Shared host scope cap=1 concurrent inquiry/synthesis, queued/rate-denied no-send until admission within deadline, hard-stop active/queued/late calls, actual/unknown usage and no double release; supported and contradicted linkage, lone candidate, unknown binding, finite candidate bounds, cycles and distinct roots with shared helpers. |
| FR-010–011; SC-003/005 | Actual input ledger/dependencies, no invented Edge, atomic BL publication failure and historical exact reads; real provider usefulness separately. |
| FR-015–016; SC-001/003/006 | Mixed source/advisory statement survives CL -> BL -> excerpt with visible qualification; graceful stop settles completed writes and records unprocessed gaps, whereas paused/unsettled/indeterminate work cannot freeze. |
| FR-012–014; SC-006 | Four-lane contract fixture, schema serialization/Chat mapping checks, log fault and redaction probes, documented static/controlled/runtime outcomes and integration handoff. |

Use [quickstart.md](quickstart.md) as the future verification guide. No build, test, provider, source scan or database migration has run for this planning change.

## Deferred work and exact stop conditions

Deferred: full 18.5/general receipt framework, durable batch restart, mutable-head ordering, cross-batch lineage/refresh, custom visibility epochs, universal protocol matching, new source tools in Pass 2, automatic/recursive scans, external operational integrations, broader UI/history/graph controls and generic-product qualification matrices. Deferral does not waive live access, bounded execution, immutable history, explicit gaps or real-source/provider proof for this slice.

Stop conditions:

1. **This session:** stop after accepted independent review. Do not generate tasks, implementation, contracts in C#, migrations or runtime fixtures. Do not run other skills, agent-context updates or edit root instructions.
2. **Before implementation:** explicit user authorization to proceed is absent. An accepted plan is not permission; address the Scan-strategy trigger below before tasks.
3. **Before consumers diverge:** allow separately authorized initial contract/fixture work and minimal compile/conformance probes; stop dependent production implementation if CP-K1 shared compiled API/fixture hash is absent or disagrees with a consumer; Business repairs the shared contract, integration propagates it. No competing types/schema.
4. **Before accepting new files:** stop if trusted admission/completion ownership, shared commit guard/current eligibility, exact binding/source validation or live producer authority is missing. A completed capability does not override a later fence/deadline. Do not accept Worker Complete flags or partial manifests.
5. **Before ordinary reads:** missing configured selection/live authority, unsupported per-user entitlement expectation, stale subject or contributor denial yields unavailable; never use operator or legacy routes as fallback.
6. **Before finalization:** pending/paused root work, unbounded inquiry, undisposed matching items or missing provider/resource authority or missing compatible host execution scope stops the affected phase. Failed terminal files and graceful-stop unprocessed/cancelled dispositions can yield explicit coverage gaps once writes settle; they do not require resuming analysis. Missing decisive linkage yields Uncertainty, not a fabricated edge.
7. **Before publication:** mismatched dependencies, unauthorized contributor or incomplete atomic validation stops promotion. Existing accepted/history data remains. Unknown commit status requires lookup; unavailable lookup remains indeterminate.
8. **Before schema/runtime work:** migration ownership conflict, unapproved database/provider/source/log target, shared-file edit or package/tool change stops the affected action for owner coordination. Never change PATH/global settings or unrelated processes/databases.
9. **Before completion claims:** unavailable real-provider/source/PostgreSQL/integrated evidence stays an explicit qualification gap; fixtures are never substituted for it.

## Planned-change approval assessment

- **Scan strategy: YES, conservatively.** B1/B2 introduce fixed-corpus candidate selection, bounded matching inquiry, rooted grouping, finalization execution/admission/accounting and explicit fence-versus-commit-submission ordering. Before tasks, explicit approval should cover these strategies and their effects on uncertainty/coverage, even though Scan owns the caller/scheduler. References: this plan Design items 6 and B1/B2; contract resolution limits; PRD FR-6.2/6.3, FR-4.7 and architecture AD-9–13/20. Intent is later implementation of this bounded document-first pipeline, not a full generic scheduler.
- **Agent tools: NO new exposed tool in this lane as planned.** Shared read ports are application APIs; resolution/BL receive bounded accepted inputs without new tools. Chat's wiki wrappers and any future source/verification tool are separate owner plans and require tool-trigger review before exposure. Adding one here changes this assessment and stops work for approval.
- **PRD: NO new amendment proposed.** Existing October first-deliverable inputs explicitly narrow delivery, not the generic obligations. FR-1.4/4.7/6.1–6.3/7.3/11.1/11.4/11.6 and NFR-2.3/3.3 remain binding where applicable. The explicit coarse reader policy adds no fine-grained RBAC. A different entitlement policy or weakening live access/BL-only/exact publication would require a new decision and applicable PRD approval.

The user requested reporting triggers and stopping, not starting the next approval phase. No approval is inferred from independent plan acceptance or a historical delegate.

## Complexity Tracking

No new project, context, service, workflow engine or generic receipt framework. The direct-SpecKit routing exception is explicitly user-authorized. New October types/storage are necessary because legacy ScanRun/Git/current-head contracts are incompatible; adapting them by fabricated origins was rejected. Finite limits and explicit immutable publication avoid generic lifecycle machinery while preserving compatible invariants.



# 2026-10-11 amendment: one local-flow BL publication

User approved the complete `october-minimal-bl-first-publication-2026-10-11.md`
management handoff. This slice supersedes the broader tool-assisted proposal:
one rich retained registration root, one compact exact member-evidence packet,
existing canonical publication and ordinary non-management Business reader.
Cross-repository completeness remains deferred and explicitly unconfirmed.

Separate finite owner-input/gap/result bounds from model/proposal limits. Preserve
the entire owner lineage and gaps, but expose only complete flow-member claims to
the model; independently reject unsupplied references in the publication policy.
Retain historic stored-result replay. Synthesis reserves measured input plus output,
allows the CL-proven 393216 output ceiling and up to three staged structural
corrections within one finite deadline/request budget, with no outer retry.

Owned changes: Business synthesis policy/executor/prompt, focused tests and this
amendment. User separately authorized Business-only request-local thinking and
reasoning-replay changes in `OctoberWorkerClientFactory.cs`; its CL streaming path
stays unchanged. No tools, resolution-policy change, rescan, schema/migration,
alternate publication, Web/UI changes, merge or push. One focused independent
review follows focused tests. Paid calls and retained writes still require exact
resource and recalculated-budget authority. Four-hour checkpoint: 2026-10-11
23:31 Asia/Shanghai (session began 19:31). Stop after first verified publication.

