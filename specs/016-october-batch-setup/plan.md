---
governed_delivery_classification: architecture
governed_delivery_reason: Adds a dedicated October persistence boundary and a shared generator-Web exact-selection contract under O-FR17/18 and O-AR03/04.
---

# Implementation Plan: Owned October Batch Setup

**Branch**: `016-october-batch-setup` (feature selector in an assigned detached managed worktree) | **Date**: 2026-09-28 | **Spec**: [spec.md](spec.md)

**Input**: October Story 18.4. Definition and planning only. Completion dependency 18.1 remains unverified in this worktree; no tasks or implementation follow this plan until integration and reconciliation.

## Summary

Prepare one October batch in one explicitly authorized PostgreSQL database using a dedicated, fixed-schema EF context. Both generator and explicitly enabled October Web bind that exact database and batch at process startup through one shared setup/selection contract. Persist real operator and valid System/Repository selection plus separate Global/System/Repository advisory values and origins; bind complete provider profiles from one explicitly declared configuration source per process. Ordinary Web composition, Identity/domain contexts, migrations and routes keep their existing behavior. Repository/root/source identity and contained read-only access remain the accepted Story 18.1 contract, to be integrated and reconciled before task generation.

## Technical Context

**Language/Version**: C# / .NET 10 (`Vulgata.Infrastructure.csproj`, `tests/Vulgata.Tests.csproj`).

**Primary Dependencies**: EF Core 10.0.9, Npgsql EF provider 10.0.3, existing ASP.NET Core Identity/Data Protection, FluentValidation 11.3.1; versions from `Directory.Packages.props`. No new package is selected.

**Storage**: PostgreSQL 17 target; one owned database per batch, identical fixed `october` schema/model, distinct October migration set/history/snapshot. Normal Identity and `vulgata` contexts remain on their ordinary configured database. No schema search-path switching.

**Testing**: Existing `tests/Vulgata.Tests` xUnit project. Controlled setup/selection/authorization/DI-model tests plus two explicitly owned PostgreSQL test databases for real schema, migration and cross-database isolation. SQLite tests alone cannot establish PostgreSQL isolation.

**Target Platform**: Existing .NET Web host and a future October generator composition; adopted two-service Docker deployment remains a later qualification target. No generator executable was found in current `src/dotnet`, so its composition must use the shared contract when introduced, rather than assuming an existing host is complete.

**Project Type**: Modular monolith; Core owns setup policy/ports, Infrastructure owns October persistence, Web owns host composition and the existing Identity exception. Agents get injected contracts and no database/path authority.

**Performance Goals**: Startup rejects invalid selection before any October batch data read/write; no numerical throughput target is established by Story 18.4.

**Constraints**: Explicit owned target and real permitted operator; fixed startup selection; no fallback, silent provider mixing, fabricated Git revision, copied legacy Knowledge, dummy pair/FlowGroup, or forced ScanRun. Credentials and raw source are absent from diagnostics.

**Scale/Scope**: One selected batch per host process and one owned database per batch; System member and standalone Repository cases; separate Worker and Chat role bindings.

## Constitution Check

*GATE: evaluated before research and again after Phase 1 design.*

| Principle | Disposition |
| --- | --- |
| I. Layers/ownership | Pass as proposed: Core setup rules/ports, Infrastructure October context and adapters, Web composition/Identity. The one setup writer is shared by generator and Web; later Knowledge acceptance remains with Story 18.5. |
| II. Domain/terminology | Pass as proposed: System is organizational; advisory context is not source proof; no legacy pair, ScanRun, or mutable Document Version is imported. |
| III. Validation/API | Pass as proposed: validated programmatic setup, no new CRUD UI/API required; if an API is later approved, existing 401/403 and ProblemDetails rules govern it. |
| IV. Verification | Pass as a plan, not a test result: honest behavioral TDD later, controlled cross-target checks, PostgreSQL migration checks and ordinary-startup regression. |
| V. Security | Pass as proposed: authorization before setup, protected secrets, no model-selected DB/path/credential, sanitized outcomes. Actual owned resources and runtime remain unverified. |

No constitution exception is proposed. The fixed October schema/context decision follows the approved October proposal's O-FR17 and O-AR03, not a revision of the normal model. The unresolved 18.1 source seam is an explicit delivery hold, not an assumed successful gate.

## Implementation Design

### Responsibilities

- **ID-R01**: A Core setup policy accepts a trusted authorization result rather than a request-supplied operator or connection string, validates exact batch selection, System/Repository membership (including standalone Repository) and context provenance, then invokes one Infrastructure setup writer. Generator and October Web call this same policy/port. Web owns the existing Identity-backed authorization adapter; source access remains a separate 18.1 capability.
- **ID-R02**: Infrastructure owns a dedicated October EF context/configuration/migrations/history and one selected-data adapter; Web and generator compose it only for October mode. The normal context, Identity placement and ordinary startup remain their current owners.
- **ID-R03**: A host-local configuration binder resolves each declared Worker and Chat provider role as one complete profile from one declared source with visible precedence; it never chooses a provider or credential on behalf of an agent.

### Interfaces

- **ID-I01**: Shared programmatic setup input carries only an allowlisted target ID, exact batch ID, selected organizational identities and applicable context selections. The operator subject and resolved database connection come from ID-I05's trusted authorization/target resolution, never these caller-controlled fields; a substituted subject, target alias, connection string or physical source path is rejected. Result is a sanitized selectable/not-selectable outcome. See [contracts/batch-setup-selection.md](contracts/batch-setup-selection.md).
- **ID-I02**: Each host receives an immutable startup selection of database target plus batch ID. Before resolving any October data service, bind the target, verify the batch exists in that database and matches the setup identity, then expose only that bound selector to October consumers. Missing or mismatched selection fails startup without fallback. Existing ordinary Web connection/Identity configuration is separate.
- **ID-I03**: Provider binding input declares source, precedence, API type, endpoint/model/credential reference and Worker/Chat role profile identities. Validate each effective profile atomically; do not fill absent fields from another profile/source. No unspecified `.env` discovery or DB-row copy is part of the provisional route. See [contracts/provider-binding.md](contracts/provider-binding.md).
- **ID-I04**: The prepared-source association is a deferred adapter consuming 18.1's integrated accepted Repository/root/source identity, authorization grant and contained read-only tool contract. Its names, storage representation, identity cardinality and validation rules are deliberately unspecified here; the setup must not expose a usable source binding until those actual contracts are reconciled.
- **ID-I05**: An ordinary Web management endpoint uses the existing authenticated Identity principal and live `ManagementAccess` policy to issue a short-lived, purpose-bound protected batch-activation grant for the exact operator subject, allowlisted target ID, batch ID and selected Repository IDs. Web verifies it locally; the generator presents it to a Web authorization endpoint that revalidates its protection/expiry, current Identity role, Repository visibility and target allowlist before returning only a sanitized authorization result. Both hosts resolve the target ID from deployment-operator-controlled local allowlists with the same canonical database identity and verify the connected database identity read-only before migration; reachability or a caller string never establishes ownership. For a System member, require current visibility through the existing System-owner filter; for a standalone Repository, require current `ManagementAccess`, a real Repository with `SystemId == null`, and no invented System owner. This authorizes setup/selection only, never 18.1 source reads.

### State and Lifetime

- **ID-S01**: The database target, exact batch ID and effective provider-role profiles are fixed for one host process lifetime; changing any selection requires restart. No per-request or live-turn database switch is offered.
- **ID-S02**: October setup rows are durable only after one transaction commits all setup-owned metadata; a failed or cancelled setup leaves no selectable batch. The protected grant is short-lived process input, not an October Identity row or permanent permission. Real operator, System/Repository, target and context authority is checked before migration/setup and again at protected use through ID-I05, since independent databases provide no cross-database foreign key or atomic authorization snapshot. An expired/revoked grant denies use and requires a newly authorized activation; context values retain scope, origin and disagreement separately. Later Knowledge records belong to subsequent stories.
- **ID-S03**: The dedicated October context has a fixed `october` schema, `october.__OctoberMigrationsHistory` history table and a separate migration folder/model snapshot; only explicitly selected October mode migrates an authorized batch database. Ordinary Web still migrates its existing Identity/domain contexts against its ordinary target; no October mapping enters `VulgataDbContext`'s assembly scan.

### Async and Cancellation

- **ID-A01**: Setup, selection validation and persistence pass cancellation through bounded async operations. Cancellation before commit rolls back setup-owned writes and releases connections; cancellation after an acknowledged commit cannot turn the committed batch into an unreported partial state. Startup failure settles before serving October requests.

### Failure and Retry

- **ID-F01**: Missing, expired, forged or substituted activation grant; unauthorized/unlisted target; revoked operator/Repository visibility; missing/mismatched batch; membership/context; provider-source/secret compatibility; and migration failures have distinct sanitized classes. A failed Web authorization check or unreachable authorization endpoint fails closed before October migration/setup/use. None selects latest/other data, falls back to legacy Knowledge, or implies source-tool/provider readiness.
- **ID-F02**: A retry after uncertain setup acknowledgement reads the exact batch in the selected database and compares its immutable setup identity; an identical request can return the existing result, while a conflicting request fails without overwriting it. Do not retry a different database or repeat provider activity. EF transient connection retry settings do not relax this identity check.

### Reuse and Locality

- **ID-L01**: Reuse existing System/Repository/GlobalContext validation, Web Identity/`ManagementAccess` and Data Protection in the Web authorization adapter; do not inject request-scoped `IUserContext` into the headless generator. Current `RepositoryAuthorizationVerifier` requires a System and cannot decide standalone setup, while `IContextCompositionService` returns flattened text and cannot preserve per-scope origins/conflicts. Keep October-specific authorization and provenance in their owning seams rather than changing ordinary Scan/source policies.
- **ID-L02**: Keep October mappings in explicit Infrastructure scope and filter configuration discovery on both contexts; `VulgataDbContext.OnModelCreating` currently scans its assembly. Keep Web's normal `Program.cs` migration path intact and add conditional October composition only. No separate project is needed by the present dependency graph.
- **ID-L03**: Story 18.1 owns prepared-source contracts. Reuse the integrated result by adaptation after its verification; Story 18.4 neither copies its implementation nor treats Story 13.1 Git-bound snapshot semantics as prepared-origin identity.

### Test Seams

- **ID-T01**: Test setup through the shared public port with Web-authenticated permitted versus denied operators and generator grants: forged/substituted subject, target ID, connection string, batch or Repository; expired/revoked grant; unlisted/reachable database; Web authorization outage; System member versus standalone Repository; bad membership; changed authority before use; and cancellation. Assert denial before migration/setup or protected use, no selectable partial batch or fabricated pair/FlowGroup/ScanRun, and explicit context scope/origin/conflict.
- **ID-T02**: Start both compositions with two owned PostgreSQL databases using identical migrations but disjoint batch IDs; assert exact selection, mismatch rejection before October data access, no cross-target read/write and restart-only switching. Compare ordinary EF model/migration list and ordinary startup behavior before/after.
- **ID-T03**: Test provider binding with complete distinct and deliberately shared role profiles, missing/conflicting fields and protected-secret failure; inspect sanitized outputs and assert no fallback, field mixing, secret/raw-source exposure or paid provider call during setup.
- **ID-T04**: After verified 18.1 integration, test the actual accepted Repository/root/source grant and contained read-only tool adapter across valid, foreign and unavailable bindings; no substitute fixture contract can close this seam.

### Alternatives

- **ID-X01**: Reject schema-per-batch/search-path redirection: the approved October choice is database-per-batch and current EF mappings are schema-qualified. A dynamic schema model would add cache/migration complexity and risk ordinary data.
- **ID-X02**: Reject reusing `VulgataDbContext`/legacy Knowledge tables: assembly-scanned mappings, Git/pair assumptions and ordinary startup migrations would violate O-AR01–04. A dedicated context with explicit configuration filtering has a narrower migration boundary than an additional assembly at this stage.
- **ID-X03**: Reject implicit DB configuration fallback or encrypted-row copying in the provisional provider route. Explicit host-supplied configuration avoids Data Protection key/application/purpose coupling; a later chosen DB route requires its own verified compatibility and plan reconciliation.

### Consequences

- **ID-C01**: Operators must provision/authorize a distinct owned database and restart hosts to switch batches; shared setup identity prevents previous-batch fallback but requires explicit configuration for each host.
- **ID-C02**: The October context introduces a separate migration lifecycle and model-isolation regression obligation. Normal startup must keep working even when October setup is absent or broken.
- **ID-C03**: The plan remains non-deliverable until 18.1 is integrated and verified locally, its accepted contracts replace the deferred source association, and affected spec/plan content receives independent review. No task generation, source grant, provider/database execution or implementation approval is implied by this draft.

## Dependency and Reconciliation Hold

Story 18.4 can decide setup ownership, fixed database isolation, exact selection, context provenance and complete provider binding now (ID-R01–03, I01–03, S01–03). It cannot finalize ID-I04, the prepared-source relation in `data-model.md`, source-binding validation or ID-T04. Story 18.1 is actively implemented in another worktree; this checkout contains no integrated verified accepted 18.1 baseline. Do not infer its result from a story title, old Git Source Snapshot, or an in-progress branch.

Before the root permits tasks or delivery, integrate the verified 18.1 baseline into this assigned checkout under root authority. Inspect its accepted specification/plan/review, production contract/API, tests and verification evidence; map exact Repository/root/source identity, permission scope, contained read-only tool grant, failure/cancellation and persistence requirements into ID-I04 and the contracts here. Check identity cardinality and cross-database association, authorization at setup and use, loss/revocation, independent repository membership and absence of fake revisions. Reconcile FR-011/SC-006 and any changed assumptions through their earliest owner, rerun deterministic plan validation and affected independent plan/definition review, then apply the existing planned-change and later implementation approval gates. No plan wording establishes the dependency by itself.

## Requirement and Success Coverage

| Requirement | Design coverage |
| --- | --- |
| FR-001 | R01, I01, I05, S02, T01 |
| FR-002 | R01, I01, I05, S02, T01; source association held at I04 |
| FR-003 | S02, L01, T01 |
| FR-004 | R01, S02, T01 |
| FR-005 | I02, F01, T02 |
| FR-006 | S01, S03, T02 |
| FR-007 | R03, I03, T03 |
| FR-008 | I03, F01, T03 |
| FR-009 | R02, S03, L02, T02 |
| FR-010 | I01, F01, T03 |
| FR-011 | I04, L03, T04; 18.1 hold |
| FR-012 | R01, I02, L02, T02; later Knowledge writer held to 18.5 |
| SC-001 | T01, T02; full source binding held to 18.1 |
| SC-002 | T02 |
| SC-003 | T01, T03 |
| SC-004 | S03, T02 |
| SC-005 | F01, T03 |
| SC-006 | I04, T04 and explicit reconciliation hold |

## Planned-Change Trigger Assessment

This plan changes a shared persistence boundary and generator/Web selection contract, so `architecture` classification and early independent plan-only review apply. It proposes no new/materially changed Worker Agent tool, Scan strategy or PRD requirement. Therefore the three AGENTS.md planned-change triggers are not established by current plan content; the root must reassess after the actual 18.1 integration/reconciliation and any review finding. Architecture decisions still require the installed early plan review and applicable gate handling. This assessment is not approval.

## Project Structure

### Documentation (this feature)

```text
specs/016-october-batch-setup/
  spec.md
  plan.md
  research.md
  data-model.md
  quickstart.md
  contracts/batch-setup-selection.md
  contracts/provider-binding.md
```

### Source Code (repository root)

```text
src/dotnet/Vulgata.Core/                 # setup policy and specific ports
src/dotnet/Vulgata.Infrastructure/Data/  # dedicated October context, configurations, migrations
src/dotnet/Vulgata.Web/                  # conditional October composition; existing Identity owner
tests/Vulgata.Tests/                     # existing controlled/integration harness
```

**Structure Decision**: Use the current projects. The future generator must consume the same Core/Infrastructure setup and selection contract; its executable location is not decided before that owner exists. Do not introduce an assembly solely to shelter mappings when explicit configuration registration can isolate them.

## Complexity Tracking

No constitution violation or exception is proposed. Dedicated persistence is required by O-AR03; explicit normal/October configuration filtering is the compatibility safeguard, not a waiver.
