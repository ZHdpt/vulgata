---
governed_delivery_classification: architecture
governed_delivery_reason: Adds a dedicated October persistence boundary and a shared generator-Web exact-selection contract under O-FR17/18 and O-AR03/04.
---

# Implementation Plan: Owned October Batch Setup

**Branch**: `oct14/story-18-4` (existing mapping `specs/017-october-batch-setup`) | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: October Story 18.4 and the completed, integrated Story 18.1 baseline at `3632b2cc`. This corrected plan requires deterministic validation and independent review before downstream gates.

## Summary

Prepare the first October batch in one explicitly authorized PostgreSQL database using a dedicated, fixed-schema EF context. An explicitly selected authorization-only October Web bootstrap starts against ordinary Identity/Data Protection and deployment allowlists, issues and validates the setup grant, and never opens an October database. The generator's `prepare` mode then validates the grant and connected target, migrates that owned empty database and atomically creates the first batch. Thereafter, generator `select` and selected-data October Web bind the exact existing database and batch through one shared selector. Persist real operator and valid System/Repository selection plus separate Global/System/Repository advisory values and origins; bind complete provider profiles from one explicitly declared configuration source per process. Ordinary Web composition, Identity/domain contexts, migrations and routes keep their existing behavior. A versioned opaque prepared-root binding per selected Repository supplies the production association to Story 18.1's already accepted approval, issuer and contained read-only tools; the generator resolves the physical directory only from deployment-owned configuration.

## Technical Context

**Language/Version**: C# / .NET 10 (`Vulgata.Infrastructure.csproj`, `tests/Vulgata.Tests.csproj`).

**Primary Dependencies**: EF Core 10.0.9, Npgsql EF provider 10.0.3, existing ASP.NET Core Identity/Data Protection, FluentValidation 11.3.1; versions from `Directory.Packages.props`. No new package is selected.

**Storage**: PostgreSQL 17 target; one owned database per batch, identical fixed `october` schema/model, distinct October migration set/history/snapshot. Normal Identity and `vulgata` contexts remain on their ordinary configured database. No schema search-path switching.

**Testing**: Existing `tests/Vulgata.Tests` xUnit project. Controlled setup/selection/authorization/DI-model tests plus two explicitly owned PostgreSQL test databases for real schema, migration and cross-database isolation. SQLite tests alone cannot establish PostgreSQL isolation.

**Target Platform**: Existing .NET Web host plus an in-scope minimal `Vulgata.OctoberGenerator` .NET executable that composes the shared setup/selector and exposes only bounded `prepare` and `select` startup modes. No generator executable exists yet in `src/dotnet`; this story must add the executable for its two-host acceptance checks. Source analysis, Worker dispatch, JDT-LS, CodeGraph, Knowledge generation and two-service Docker qualification remain later work.

**Project Type**: Modular monolith; Core owns setup policy/ports, Infrastructure owns October persistence, Web owns host composition and the existing Identity exception. Agents get injected contracts and no database/path authority.

**Performance Goals**: Authorization-only bootstrap rejects invalid grant requests without an October connection; selected-data startup rejects invalid selection before downstream October data access. No numerical throughput target is established by Story 18.4.

**Constraints**: Explicit owned target and real permitted operator; fixed startup selection; no fallback, silent provider mixing, fabricated Git revision, copied legacy Knowledge, dummy pair/FlowGroup, or forced ScanRun. Credentials and raw source are absent from diagnostics.

**Scale/Scope**: One selected batch per host process and one owned database per batch; System member and standalone Repository cases; separate Worker and Chat role bindings.

## Constitution Check

*GATE: evaluated before research and again after Phase 1 design.*

| Principle | Disposition |
| --- | --- |
| I. Layers/ownership | Pass as proposed: Core setup rules/ports, Infrastructure October context and adapters, Web composition/Identity. The one setup writer is shared by generator and Web; later Knowledge acceptance remains with Story 18.5. |
| II. Domain/terminology | Pass as proposed: System is organizational; advisory context is not source proof; no legacy pair, ScanRun, or mutable Document Version is imported. |
| III. Validation/API | Pass as proposed: no new CRUD UI, but this plan does add the narrowly scoped Web grant and internal current-authority APIs in [the shared contract](contracts/batch-setup-selection.md). Their plural-resource routes, validated closed requests, cookie versus client-certificate authentication, 401/403, ProblemDetails and antiforgery behavior follow the existing API convention. |
| IV. Verification | Pass as a plan, not a test result: honest behavioral TDD later, controlled cross-target checks, PostgreSQL migration checks and ordinary-startup regression. |
| V. Security | Pass as proposed: authorization before setup, protected secrets, no model-selected DB/path/credential, sanitized outcomes. Actual owned resources and runtime remain unverified. |

No constitution exception is proposed. The fixed October schema/context decision follows the approved October proposal's O-FR17 and O-AR03, not a revision of the normal model. Story 18.1's completed issuer and tool contracts are present; this Story 18.4 production approval and binding adaptation still requires review and implementation evidence.

## Implementation Design

### Responsibilities

- **ID-R01**: A Core setup policy accepts a trusted authorization result rather than a request-supplied operator or connection string, validates exact batch selection, System/Repository membership (including standalone Repository), context provenance and one opaque prepared-root binding per selected Repository, then invokes one Infrastructure setup writer. Generator `prepare` and any selected-data Web setup call this same policy/port; authorization-only bootstrap supplies trusted authority but never invokes October setup or persistence. Web owns Identity-backed setup authorization and live prepared-source approval; Story 18.1 continues to own source access.
- **ID-R02**: Infrastructure owns a dedicated October EF context/configuration/migrations/history and one selected-data adapter. Web `Program.cs` owns one closed startup-mode selection from host configuration before service registration, migration or route mapping, and passes it to `VulgataWebComposition.ConfigureServices(builder, selection)`; the same selected mode gates its existing startup and endpoint mapping. This story adds a minimal `Vulgata.OctoberGenerator` executable and two explicit October Web compositions: authorization-only bootstrap, with ordinary Identity/Data Protection plus deployment target/certificate allowlists and only grant/validation routes; and selected-data mode, which requires the exact existing target/batch before exposing October data services or source-approval routes. Generator `prepare` invokes the shared Core setup policy without requiring an existing batch; generator `select` and selected-data Web invoke the same Infrastructure selector. The default ordinary Web composition remains unchanged and has no October route/context/migration registration.
- **ID-R03**: A host-local configuration binder resolves each declared Worker and Chat provider role as one complete profile from one declared source with visible precedence; it never chooses a provider or credential on behalf of an agent.

### Interfaces

- **ID-I01**: The one callable Core setup policy is `OctoberBatchSetupPolicy(IOctoberSetupWriter writer).PrepareAsync(OctoberSetupRequest request, OctoberAuthorizedSelection authority, CancellationToken cancellationToken) -> Task<OctoberPrepareResult>`, with the exact construction and closed result shapes in [contracts/batch-setup-selection.md](contracts/batch-setup-selection.md). The writer is the accepted Infrastructure persistence port, not another setup policy. Request input carries only an allowlisted target ID, exact batch ID, selected organizational identities, applicable context record references and opaque per-Repository root-binding versions/labels. Web and policy use the same canonical selection digest; duplicate, changed or substituted selections fail. Operator subject and canonical database identity come only from the trusted authorization result, never request-controlled fields. `Created` or `AlreadyIdentical` returns the exact committed selectable batch identity; every other closed status returns no batch capability or identity. A substituted subject, target alias, connection string or physical source path is rejected.
- **ID-I02**: Selected-data October Web and generator `select` receive an immutable startup selection of database target plus batch ID. The shared `IOctoberBatchSelector.BindAsync(OctoberStartupSelection, CancellationToken)` returns a closed selected/rejected result and, only on success, a bound October setup-data capability; it never returns a caller-selected connection string. It verifies that the exact batch already exists in the selected database before exposing downstream October services; missing or mismatched selection fails without fallback. Generator `prepare` is the separate first-batch path: after authorization-only bootstrap grant validation, it checks the allowlisted connected target identity, conditionally migrates that explicitly owned empty database and calls the shared `PrepareAsync` setup policy to create the batch atomically. It does not call `BindAsync` or require a pre-existing batch before that commit. The generator emits only sanitized target/batch/status and exits nonzero on denial; it performs no generation or Worker dispatch. See [the shared contract](contracts/batch-setup-selection.md).
- **ID-I03**: Provider binding input declares source, precedence, API type, endpoint/model/credential reference and Worker/Chat role profile identities. Validate each effective profile atomically; do not fill absent fields from another profile/source. No unspecified `.env` discovery or DB-row copy is part of the provisional route. See [contracts/provider-binding.md](contracts/provider-binding.md).
- **ID-I04**: One durable source-binding row per `(BatchId, RepositoryId)` holds a deployment-issued opaque `RootBindingIdentity` version and descriptive `OperatorLabel`; it holds no `PhysicalRootPath`, source bytes, live context or continuation. The selected October database owns this row, while the generator's deployment-owned configuration immutably maps the exact `(target identity, BatchId, RepositoryId, RootBindingIdentity)` to one contained physical prepared directory; any mapping change requires a new version. For each direct source-read issuance, trusted composition creates the existing Core `PreparedSourceApproval(BatchId, RepositoryId, SubjectId, WorkerSourceRead, ApprovalIdentity, RootBindingIdentity, OperatorLabel)` from the selected batch, real Web Identity subject, a current short-lived approval record and the matching root binding; it then supplies `PreparedSourceGrantRequest(Approval, PhysicalRootPath, Limits)` to `PreparedSourceContextIssuer.IssueAsync`. Neither an operation argument nor a batch ID alone can select a root. The approval tuple is not a Git revision or Source Snapshot. See Story 18.1's [accepted source contract](../016-contained-source-tools/contracts/prepared-source-tools.md).
- **ID-I05**: The explicitly enabled authorization-only October Web bootstrap uses its ordinary Identity principal and live `ManagementAccess` policy to issue a short-lived, purpose-bound protected batch-activation grant for the exact operator subject, allowlisted target ID, batch ID and selected Repository IDs. It exposes the grant and generator validation routes without selecting, migrating or reading an October database; the generator presents its grant and separate client-certificate identity for live validation. Both hosts resolve target IDs from deployment-owned allowlists with the same canonical database identity; only generator `prepare` checks the connected October target before migration. For a System member, require current System-owner visibility; for a standalone Repository, require current `ManagementAccess` and `SystemId == null`. This grant authorizes setup/startup selection, not standing source disclosure. After setup, selected-data October Web can issue an opaque `ApprovalIdentity` and hold its current source-approval record in the selected October database. The generator-side `IPreparedSourceCurrentAuthorityVerifier` adapter checks selected-data Web's live record/Identity/Repository/target decision and its own immutable deployment root mapping against the requested tuple at issuance and every Story 18.1 operation. Mismatch/revocation returns `Revoked`; inaccessible authority returns `Unavailable`; neither reuses bootstrap validation as a cached allow.
- **ID-I06**: Pin the callable Web boundary in [contracts/batch-setup-selection.md](contracts/batch-setup-selection.md). `OctoberWebStartupSelector.Resolve(IConfiguration)` reads only the closed `October:Web:Mode` host key (`ordinary`, `authorization-only`, `selected-data`; absent means ordinary), rejects unknown modes or partial/conflicting mode inputs before any October service or migration, and yields immutable `OctoberWebStartupSelection`. Only selected-data accepts and requires exact `October:Web:TargetId`, `October:Web:BatchId` and protected `October:Web:ActivationGrant`; it completes live grant and `BindAsync` checks before serving. `Program` uses that selection for both `VulgataWebComposition.ConfigureServices(builder, selection)` and route/startup gating, so `WebApplicationFactory<Program>` can choose the same production composition through `ConfigureAppConfiguration`, without test-only endpoints. Authorization-only bootstrap exposes exactly operator-authenticated `POST /api/october-batch-activation-grants` and generator-authenticated `POST /api/october-batch-activation-grants/validations`; it has no selected-data or source-approval route. Selected-data October Web may expose those plus the prepared-source approval creation and exact current-authority verification routes only after exact batch binding. The concrete `OctoberGeneratorCertificateVerifier` consumes `IOctoberAuthenticatedCertificateSource` and immutable target-scoped allowlist configuration; its closed decision distinguishes authorized, unauthenticated, forbidden, authority unavailable and cancelled. Production certificate source accepts only an authenticated, unexpired trusted mutual-TLS client certificate from the TLS/auth boundary, never a cookie, body or header; controlled SelfContained tests may replace only that source and allowlist data, not the verifier. The generator also presents the short-lived operator grant where required; either proof alone is insufficient. Closed request/result fields, sanitized 400/401/403/503 ProblemDetails and cancellation/deadlines are fixed in the contract. Unavailable Web authority maps to `PreparedSourceAuthorityStatus.Unavailable`, never cached `Current`.

### State and Lifetime

- **ID-S01**: The database target, exact batch ID and effective provider-role profiles are fixed for one host process lifetime; changing any selection requires restart. No per-request or live-turn database switch is offered.
- **ID-S02**: October setup rows are durable only after one transaction commits all setup-owned metadata, including per-Repository opaque root bindings; a failed or cancelled setup leaves no selectable batch. The protected activation grant is short-lived process input, not a persistent token or permanent source permission. A source-read approval is a separate current record, keyed by `ApprovalIdentity`, bound to the exact `PreparedSourceApproval` tuple and expiring/revocable; the verifier checks it and live Web authority before disclosure. The source-binding version remains fixed for the batch; revocation/rotation closes existing contexts and invalidates continuations. A different root requires fresh authorization and an explicit new batch or separately reviewed rebind operation, never silent mutation of selected setup. Context values retain scope, origin and disagreement separately; later Knowledge records belong to subsequent stories.
- **ID-S03**: The dedicated October context has a fixed `october` schema, `october.__OctoberMigrationsHistory` history table and a separate migration folder/model snapshot. The mode selection is immutable for the Web host lifetime and is resolved before registration, migration, readiness resolution and route mapping. Authorization-only October Web bootstrap and default ordinary Web register no October context, selector, migration or selected-data service; both use only the existing ordinary Identity/domain startup path. Only generator `prepare`, after live grant validation and connected-target identity check, may migrate an explicitly owned October database and create its first batch. Selected-data Web and generator `select` never migrate and require the exact already selectable batch. No October mapping enters `VulgataDbContext`'s assembly scan.
- **ID-S04**: A successful Story 18.1 `IssueAsync` returns one `IPreparedSourceReadContext` for one `(BatchId, RepositoryId, SubjectId, Purpose, ApprovalIdentity, RootBindingIdentity)` approval. A future trusted Worker-session caller owns its `IAsyncDisposable` lifetime and disposes it after all three borrowed `PreparedSourceFunctions` invocations settle. Story 18.4 establishes the approval/binding and a direct public-seam qualification, not a Worker dispatcher. `PreparedSourceFunctions.Create()` neither transfers ownership nor issues another grant. Contexts, physical handles and one-use continuations are never persisted or reused across host restart; each Repository needs its own issuance. A failed issue status carries no context.

### Async and Cancellation

- **ID-A01**: Setup, selection validation, Web authority calls and persistence pass cancellation through bounded async operations. The generator uses an explicitly bounded HTTP request deadline and forwards caller cancellation through its authenticated Web client; cancellation never changes a denied/unavailable decision into allow. Cancellation before commit rolls back setup-owned writes and releases connections; cancellation after an acknowledged commit cannot turn the committed batch into an unreported partial state. Startup failure settles before serving October requests. The direct source-issuance seam passes caller cancellation to `IssueAsync`; a future Worker caller must forward invocation cancellation through the existing three functions, await in-flight calls, then dispose its owned context. Cancelled/deadline-expired issuance returns no context. Per-call cancellation, deadline, disposal and authority loss invalidate continuations under Story 18.1's existing contract; no resume/reissue is automatic.

### Failure and Retry

- **ID-F01**: Missing, expired, forged or substituted activation grant; unauthorized/unlisted target; revoked operator/Repository visibility or prepared-root binding; missing/mismatched batch; membership/context; provider-source/secret compatibility; and migration failures have distinct sanitized classes. A failed Web authorization check or unreachable authorization endpoint fails closed before October migration/setup/use. For prepared-source issuance, propagate Story 18.1's closed `PreparedSourceIssueStatus`: `Denied` for foreign/revoked/malformed approval, `AuthorityUnavailable` for verifier outage, `RootUnavailable` for the configured root, `UnsupportedPlatform` for unqualified containment, and `Cancelled`/`DeadlineExceeded` for their named stops. Do not turn a failed issue into a ready Worker source capability. None selects latest/other data, falls back to legacy Knowledge, or implies source-tool/provider readiness.
- **ID-F02**: A retry after uncertain setup acknowledgement reads the exact batch in the selected database and compares its immutable setup identity; an identical request returns `AlreadyIdentical` with that committed identity, while a conflicting request returns `Conflict` without overwriting it. `Denied`, `AuthorityUnavailable`, `Cancelled` and `StorageFailure` return no committed identity. Do not retry a different database or repeat provider activity. EF transient connection retry settings do not relax this identity check; a later explicit `BindAsync` is the only path to an existing-batch selected-data capability.

### Reuse and Locality

- **ID-L01**: Reuse existing System/Repository/GlobalContext validation, Web Identity/`ManagementAccess` and Data Protection in the Web authorization adapter; do not inject request-scoped `IUserContext` into the headless generator. Current `RepositoryAuthorizationVerifier` requires a System and cannot decide standalone setup, while `IContextCompositionService` returns flattened text and cannot preserve per-scope origins/conflicts. Keep October-specific authorization and provenance in their owning seams rather than changing ordinary Scan/source policies.
- **ID-L02**: Keep October mappings in explicit Infrastructure scope and filter configuration discovery on both contexts; `VulgataDbContext.OnModelCreating` currently scans its assembly. Keep Web's normal `Program.cs` migration path intact by selecting the default ordinary mode and calling its existing composition/endpoint path; explicit October modes register and map only their respective services/routes. The production certificate source belongs to Web's TLS/auth adapter, while the target-scoped verifier is shared by both October internal routes; a controlled source substitution tests that same verifier, not a parallel test authentication policy. Add one small generator executable project because Story 18.4 requires real two-host startup selection and no such host currently exists; it composes existing Core/Infrastructure modules rather than duplicating their policy or adding generation behavior.
- **ID-L03**: Story 18.1 owns `PreparedSourceApproval`, `PreparedSourceGrantRequest`, `IPreparedSourceCurrentAuthorityVerifier`, `PreparedSourceContextIssuer`, `IPreparedSourceReadContext` and the closed `PreparedSourceFunctions` facade. Story 18.4 supplies only the production approval/binding adapter and trusted composition; it neither copies their implementation nor treats Story 13.1 Git-bound snapshot semantics as prepared-origin identity.

### Test Seams

- **ID-T01**: Construct the public `OctoberBatchSetupPolicy` with a controlled `IOctoberSetupWriter` and call its single `PrepareAsync` using the declared request/authority records. Assert exact digest/target/batch/Repository checks; `Created`/`AlreadyIdentical` iff a matching committed selectable identity is returned; `Conflict`, `Denied`, `AuthorityUnavailable`, `Cancelled` and `StorageFailure` carry no identity or capability. Then select authorization-only mode through `WebApplicationFactory<Program>.WithWebHostBuilder(...ConfigureAppConfiguration(...))`, not a test route or verifier replacement, and test actual Web API with authenticated permitted versus denied operators and generator grants: forged/substituted subject, target ID, connection string, batch or Repository; expired/revoked grant; unlisted/reachable database; Web authorization outage; System member versus standalone Repository; bad membership; changed authority before use; and cancellation. Supply a controlled authenticated certificate identity and target-scoped allowlist to the same production verifier. Assert bootstrap exposes only grant issuance/validation, not source-approval or selected-data routes, and registers/opens no October target. Missing/invalid/out-of-scope generator certificate fails before protected use; 401/403 and ProblemDetails stay API-shaped without redirects/HTML, malformed closed requests fail validation, and unavailable authority maps to fail-closed 503. Assert unknown/partial mode configuration fails before serving or migration, and absent mode keeps ordinary startup. Assert denial before migration/setup or protected use, no selectable partial batch or fabricated pair/FlowGroup/ScanRun, and explicit context scope/origin/conflict.
- **ID-T02**: Start authorization-only October Web against the ordinary Identity target with a freshly created, explicitly owned empty PostgreSQL target that has no October schema or batch. Assert bootstrap routes issue/validate a real grant without any October connection, migration, selector or selected-data/source-approval registration. Run the actual generator `prepare` mode through that live route; assert connected target identity check precedes its sole authorized migration, the first batch commits atomically, and absent/forged authority leaves the empty target untouched. Then start selected-data October Web and generator `select` against exact A/A; repeat with a second owned B/B target, missing target/batch and A/B mismatch. Assert shared selector use only after batches exist, no downstream or cross-target access on failure, nonzero generator exit, restart-only switching, and ordinary Web model/migration/startup regression. An in-process selector unit test or preseeded batch alone cannot satisfy this first-batch/two-host acceptance seam.
- **ID-T03**: Test provider binding with complete distinct and deliberately shared role profiles, missing/conflicting fields and protected-secret failure; inspect sanitized outputs and assert no fallback, field mixing, secret/raw-source exposure or paid provider call during setup.
- **ID-T04**: Through the real `PreparedSourceContextIssuer` public seam, use a controlled current-authority adapter over setup/approval state and an owned prepared root. Assert exact `PreparedSourceApproval` fields, one source binding per `(BatchId, RepositoryId)`, rejection of foreign batch/Repository/subject/purpose/approval/root version and unavailable authority, and no context on every failed `PreparedSourceIssueStatus`. Construct `PreparedSourceFunctions` only from an issued context; assert exactly `list_prepared_source`, `read_prepared_source`, `search_prepared_source` with no model-selected root/authority field. Revoke/rotate binding or subject permission before issuance, between calls and before disclosure; assert denied/authority-unavailable with no payload or continuation, no reactivation of the old context, cancellation forwarding, settled calls and caller disposal. Reuse Story 18.1's existing public-seam/native-containment tests as regression evidence without duplicating its path, schema or search policy tests.

### Alternatives

- **ID-X01**: Reject schema-per-batch/search-path redirection: the approved October choice is database-per-batch and current EF mappings are schema-qualified. A dynamic schema model would add cache/migration complexity and risk ordinary data.
- **ID-X02**: Reject reusing `VulgataDbContext`/legacy Knowledge tables: assembly-scanned mappings, Git/pair assumptions and ordinary startup migrations would violate O-AR01–04. A dedicated context with explicit configuration filtering has a narrower migration boundary than an additional assembly at this stage.
- **ID-X03**: Reject implicit DB configuration fallback or encrypted-row copying in the provisional provider route. Explicit host-supplied configuration avoids Data Protection key/application/purpose coupling; a later chosen DB route requires its own verified compatibility and plan reconciliation.
- **ID-X04**: Reject a test-only generator stub or a second selection implementation: neither proves the executable's startup boundary required by O-FR17. The minimal console host adds packaging/composition work now, while later generation remains outside this story.

### Consequences

- **ID-C01**: Operators must provision/authorize a distinct owned database and restart hosts to switch batches; shared setup identity prevents previous-batch fallback but requires explicit configuration for each host.
- **ID-C02**: The October context introduces a separate migration lifecycle and model-isolation regression obligation. The authorization-only bootstrap must be operable before any October batch exists, while selected-data startup must refuse an absent batch. Normal startup must keep working even when October setup is absent or broken.
- **ID-C03**: Story 18.1 is completed and integrated at `3632b2cc`, with its contract and final verification linked below. The current Story 18.4 specification reflects that baseline. This plan adds a production approval registry and source-binding adapter; neither Story 18.1's direct qualification nor plan text proves that adaptation works. This corrected plan must pass deterministic validation and repeat independent plan review before tasks.
- **ID-C04**: Internal Web authority routes require deployment-owned client-certificate identity and an explicitly trusted TLS boundary in addition to the operator's short-lived grant. Operators must provision and rotate that identity for both hosts; absent/expired/untrusted client proof fails closed. Controlled tests can use owned certificates, while production certificate and database access require separate explicit authorization and qualification.

## Integrated Source Boundary and Review

The completed Story 18.1 baseline entered this branch through merge checkpoint `f190f8dc`; see its [source contract](../016-contained-source-tools/contracts/prepared-source-tools.md), [Core types](../../src/dotnet/Vulgata.Core/DomainServices/PreparedSourceContracts.cs), [issuer](../../src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs), [closed functions](../../src/dotnet/Vulgata.Agents/WorkerExecution/PreparedSourceFunctions.cs) and [final verification](../016-contained-source-tools/evidence/final-verification-01.json). Its DB-free qualification establishes the source capability itself. The current Story 18.4 specification reflects that integration. This plan specifies Story 18.4's production source approval, binding cardinality, callable Web authority API and minimal generator startup at ID-I02/I04/I05/I06, S02/S04, A01, F01 and T01/T02/T04. The corrected plan still requires deterministic validation and independent review; baseline integration alone is not approval or adapter qualification.

## Requirement and Success Coverage

| Requirement | Design coverage |
| --- | --- |
| FR-001 | R01, I01, I05, I06, S02, T01 |
| FR-002 | R01, I01, I04, I05, S02, T01, T04 |
| FR-003 | S02, L01, T01 |
| FR-004 | R01, S02, T01 |
| FR-005 | I02, F01, L02, T02 |
| FR-006 | S01, S03, T02 |
| FR-007 | R03, I03, T03 |
| FR-008 | I03, F01, T03 |
| FR-009 | R02, S03, L02, T02 |
| FR-010 | I01, F01, T03 |
| FR-011 | I04, I05, I06, S04, L03, T04; integrated 18.1 contract |
| FR-012 | R01, I02, L02, T02; later Knowledge writer held to 18.5 |
| SC-001 | T01, T02, T04 |
| SC-002 | T02 |
| SC-003 | T01, T03 |
| SC-004 | S03, T02 |
| SC-005 | F01, T03 |
| SC-006 | I04, I05, I06, S04, T04 and integrated 18.1 verification; repeat Story 18.4 review pending |

## Planned-Change Trigger Assessment

This plan changes a shared persistence boundary, adds a minimal generator startup host and pins generator/Web authorization routes, so `architecture` classification and repeat independent plan-only review apply. It composes Story 18.1's already accepted three Worker functions without adding, exposing or materially changing their arguments, outcomes or dispatch, and adds no Scan strategy or PRD change. On that scope, the three AGENTS.md planned-change triggers are not established. An independent reviewer and root must reassess this corrected boundary; any expansion of the Worker tool surface or source strategy changes the trigger and requires the named planned-change authorization before tasks. This assessment is not approval.

## Project Structure

### Documentation (this feature)

```text
specs/017-october-batch-setup/
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
src/dotnet/Vulgata.OctoberGenerator/     # minimal prepare/select executable, no generation loop
tests/Vulgata.Tests/                     # existing controlled/integration harness
```

**Structure Decision**: Add one minimal generator executable project in this story so its actual startup calls the same Core/Infrastructure setup and selector as October Web. Keep shared policy and persistence in their existing owning projects. Do not introduce an assembly solely to shelter mappings when explicit configuration registration can isolate them; later generator analysis features extend this executable under their own stories.

## Complexity Tracking

No constitution violation or exception is proposed. Dedicated persistence is required by O-AR03; explicit normal/October configuration filtering is the compatibility safeguard, not a waiver.
