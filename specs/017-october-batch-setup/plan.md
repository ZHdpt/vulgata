---
governed_delivery_classification: architecture
governed_delivery_reason: Replaces the online Web authority dependency with shared in-process Identity and database authority while retaining isolated October persistence and startup-only selection.
---

# Implementation Plan: Owned October Batch Setup

**Branch**: `oct14/story-18-4` | **Feature**: `specs/017-october-batch-setup` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Story 18.4, integrated Story 18.1, and the user's [independent batch direction](evidence/independent-batch-direction-01.json). This bounded correction changes deployment/authority composition; historical approvals and implementation remain evidence for their original boundaries. Affected gates must bind this revised scope before implementation resumes.

## Summary

The batch executable runs independently. It connects directly to explicitly configured PostgreSQL authority and batch databases, authenticates a real operator through existing Identity loaded in-process, and uses shared setup/storage rules. It requires no running Web process, Web URL, activation grant, client certificate or HTTP authority service. October Web chooses an exact database and batch in startup configuration and retains that selection until restart.

The default selected database contains separate `identity`, `vulgata` and `october` schemas with unchanged ownership. Changing Web's `ConnectionStrings:DefaultConnection` and exact target/batch metadata before restart changes the dataset it serves. Management and October always use that selected database. An explicitly named separate Identity connection is an optional deployment configuration; it never keeps management reads on a previous database or introduces a central Web prerequisite. No records or credentials are copied implicitly.

Headless authentication reuses existing Identity password, eligibility and required second-factor rules. Credentials come from an explicit protected source or no-echo operator input, never command-line arguments. This replacement of the previous grant arrangement is a consequential architecture proposal requiring the affected approvals. Web's startup operator establishes setup selection only; browser users still authenticate separately and receive only their own current permissions.

## Technical Context

**Stack**: C#/.NET 10, EF Core 10.0.9, Npgsql EF provider 10.0.3, existing ASP.NET Core Identity/Data Protection. No new package or authentication product.

**Storage**: PostgreSQL 17; one owned database per batch, identical fixed `october` schema, `october.__OctoberMigrationsHistory`, separate migrations/snapshot. Existing `identity`/`vulgata` mappings and histories remain unchanged. Co-location does not merge models.

**Hosts**: Existing `Vulgata.OctoberGenerator` with bounded `prepare`/`select`; Web with `ordinary`/`selected-data`. The previous `authorization-only` mode and four October grant/approval HTTP routes are superseded. Analysis, Worker dispatch, JDT-LS, CodeGraph and Knowledge generation remain excluded.

**Ownership**: Core owns policy/contracts; Infrastructure owns October persistence/source adapters. Identity types/migrations remain in Web/Data. A reusable headless module there is referenced by generator through an explicit generator-to-Web assembly reference, without executing Web Program or starting a listener. Web never references generator. This deliberate reuse avoids moving or duplicating Identity ownership.

**Verification**: Classified SelfContained public-seam tests, actual Program-host configuration tests, separately authorized owned PostgreSQL/process qualification. No TLS-resource prerequisite remains for removed transport. Prior transport GREEN does not establish independent execution.

## Constitution Check

| Principle | Disposition |
| --- | --- |
| I. Ownership | Core domain-only; Infrastructure owns October; existing Web/Data Identity owner supplies headless reuse. No duplicate table/writer policy. |
| II. Domain | Real identity, standalone Repository and immutable advisory provenance; no fake Git identity, pair, ScanRun or Knowledge. |
| III. Interfaces | No new HTTP protocol; existing ordinary API conventions unchanged. Exact headless constructions/results/lifetimes are fixed below and in the contract. |
| IV. Verification | Honest RED/GREEN for changed authority/host behavior; retained correct behavior uses regression. PostgreSQL/process evidence remains required. |
| V. Security | Existing Identity authentication, live subject/purpose/Repository/root authority, protected secrets and sanitized failures. |

No constitution exception is proposed. October's accepted host-execution scope removes a generic Docker prerequisite, not authorization. Co-locating schemas changes no model owner.

## Implementation Design

### Responsibilities

- **ID-R01**: Retain one `OctoberBatchSetupPolicy(IOctoberSetupWriter)`. The shared direct authority adapter authenticates a real operator and reads current Identity/organization/context from explicitly configured databases. Core validates trusted `OctoberAuthorizedSelection.SelectedContext`, exact target/batch/Repository/root/reference/digest agreement; Infrastructure atomically commits only October setup records. Request subject IDs and successful DB connections never prove operator identity.
- **ID-R02**: Both hosts consume the same authority, policy, writer and selector. Generator prepare verifies connected identity and reads existing Identity/management authority before any October migration, then migrates only October and creates the first batch without Web or a pre-existing batch. Authority reads may open the same physical database because the default schemas are co-located. Select and selected-data Web require an existing exact batch and never migrate October. Web freezes final startup configuration before provider construction. Ordinary composition/migrations/seeding/routes remain unchanged.
- **ID-R03**: A host-local binder resolves complete Worker/Chat profiles from one explicitly declared source with visible precedence; agents never choose credentials/providers.

### Interfaces

- **ID-I01**: Keep `OctoberBatchSetupPolicy.PrepareAsync(OctoberSetupRequest, OctoberAuthorizedSelection, CancellationToken) -> Task<OctoberPrepareResult>`, writer `CommitAsync` and existing Core records unchanged. Only Created/AlreadyIdentical return exact committed identity and first-commit UTC time; failures carry neither identity nor capability. The trusted adapter supplies subject/canonical database/context. Request input carries references only; canonical v2 `OctoberSelectionDigest.Compute` remains shared.
- **ID-I02**: Keep `IOctoberBatchSelector.BindAsync(OctoberStartupSelection, CancellationToken) -> Task<OctoberSelectionResult>` and historical snapshot capability. Fresh direct authority replaces Web validation. Before historical text/capability exposure, require stored/current ManagementAuthorityId, subject, target/batch and stable organization/root/reference equality. Same-authority advisory text edits do not overwrite history and need not match historical digest for read-only selection; prepare retry requires the full semantic identity. Generator command/module and connected-target interfaces are fixed in [the shared contract](contracts/batch-setup-selection.md).
- **ID-I03**: Provider source/precedence/API type/endpoint/model/credential reference and role profiles are explicit. Validate each effective profile atomically; no field borrowing, undisclosed environment discovery or encrypted-row copy. See [provider contract](contracts/provider-binding.md).
- **ID-I04**: Keep one durable binding per (BatchId, RepositoryId), opaque RootBindingIdentity version and descriptive label. Only deployment configuration maps exact target/batch/Repository/version to a physical directory. Trusted composition forms unchanged PreparedSourceApproval and PreparedSourceGrantRequest and calls unchanged PreparedSourceContextIssuer. The direct authority issues the exact approval after current checks; existing PreparedSourceBinding owns root lookup. No durable paths/contexts/continuations or caller-selected roots.
- **ID-I05**: The shared `IOctoberLocalAuthority` and its `OctoberLocalAuthority` Web/Data adapter replace Web transport. The shared contract pins constructor dependencies, opaque authenticated session, AuthenticateAsync, AuthorizeSelectionAsync, IssueSourceApprovalAsync, VerifySourceApprovalAsync and RevokeSourceApprovalAsync. Reuse existing Identity hash/sign-in/second-factor semantics; derive subject from the authenticated account. Fresh scopes recheck security stamp, eligibility/lockout, roles, System ownership, Repository membership, exact target and approval. No database-login-to-user mapping, request principal or cached permission is introduced. Preserve `OctoberConsoleCredentialSource()` for real-console composition and add only `OctoberConsoleCredentialSource(IOctoberCredentialTerminal terminal)` for the same concrete source. The contract fixes terminal availability, cancellable key input with explicit intercept/no-echo choice, prompt output, ownership and failures; it does not change credential/authentication policy or authorize a real terminal.
- **ID-I06**: Retain ConfigureOctoberHost, explicit DefaultServiceProviderFactory, deferred ConfigureContainer<IServiceCollection>, and one immutable OctoberWebStartupSelection resolved from final configuration. Accept absent/ordinary or selected-data; reject obsolete authorization-only/grant/Web-URL/certificate configuration. Selected-data requires exact target/batch and explicit authority/operator inputs; authentication/authorization/BindAsync finish before serving. After Build, use the same selection for all startup/routes. No temporary provider, immediate mode callback, test-only route or injected selection. Ordinary registration runs once. Explicit October authority options must use the final configured connection, never a premature ordinary default. Browser identity never inherits startup operator authority.

### State and Lifetime

- **ID-S01**: Connections, authority identity, exact batch and provider profiles are fixed until restart. Configuration reload changes none. Authorization remains live. Restart cannot rebind an existing batch to a different authority; root mapping changes require a new version.
- **ID-S02**: One transaction commits batch/repository/root/reference rows, three applicability slots per Repository, differences, ManagementAuthorityId and writer-owned SelectedAtUtc. Preserve exact values/origins and AdvisoryNotSourceProof; null differs from empty. Identical retry returns original time; management edits never rewrite selected history. Separate authority reads are not a distributed atomic transaction, including when optional Identity is on another database; protected use rechecks live authority.
- **ID-S03**: Keep separate October model/history/snapshot and configuration filtering. Ordinary mode neither registers nor migrates October. Prepare never creates a database, fabricates an Administrator, copies users/secrets or migrates/seeds authority. Valid Identity/management records are established through existing supported owners independently of Web execution. Co-location requires these records already to exist; a fresh October schema is not an empty Identity authority.
- **ID-S04**: Short-lived source approvals store exact tuple/expiry/revocation in October. Each issued context binds one Repository/approval; caller lends it to the three existing functions, awaits calls and disposes asynchronously. Operator sessions, scoped DB contexts, source handles and continuations are never persisted/reused after restart. Source reissue requires current authenticated authority and is never automatic after closure.

### Async and Cancellation

- **ID-A01**: All async seams accept cancellation and bounded deadlines; fresh scopes/EF commands are bounded. Identity methods without cancellation are awaited with the scope alive and checked before/after; no success after cancellation and no abandoned task on disposed services. Source verification respects the incoming remaining deadline. Cancellation before commit rolls back, acknowledged commit preserves success, uncertain acknowledgement uses exact replay. Story 18.1 retains its own cancellation/disposal/continuation rules unchanged.

### Failure and Retry

- **ID-F01**: Closed sanitized failures distinguish invalid configuration/selection, unauthenticated/ineligible operator, required-factor failure, denied target/membership/root, unavailable authority, absent/mismatched batch, provider/storage failure and cancellation. No alternate database/latest batch/Web grant/legacy fallback. Only exact live tuple/root returns Current; lost identity/stamp/role/membership, revoked/expired approval denies; database outage returns Unavailable and 18.1 closes the context. Preserve all existing issue statuses and empty failure payloads. Never log credentials/factors/stamps/connection secrets/context/source/physical roots.
- **ID-F02**: Identical setup replay yields AlreadyIdentical/original time; changed semantic content yields Conflict without overwrite. No automatic authentication/factor/cross-database/provider retry. Failed authentication supplies no session; explicit fresh authentication cannot resurrect a closed source context.

### Reuse and Locality

- **ID-L01**: Reuse ApplicationDbContext/ApplicationUser, Identity options/bcrypt/managers in Web/Data through a small headless registration entrypoint, without request-scoped IUserContext or full Web registration. Reuse management roles/System-owner semantics; standalone Repository requires live ManagementAccess with no fake System. Preserve context origins rather than using flattened composition.
- **ID-L02**: Reuse policy/digest, October store/migrations, selector, root binding and deferred configuration. Replace online grant/certificate route wiring and OctoberWebAuthorityClient; preserve historical evidence rather than keeping transport as a fallback. Generator never invokes Web Program or full application composition.
- **ID-L03**: Story 18.1 owns approval/grant/verifier/issuer/context and the closed three functions. Change only October's authority callback from HTTP to live DB checks; no copied source policy, schema, tool or dispatcher.

### Test Seams

- **ID-T01**: Retain policy/provenance/digest/writer regression tests. Exercise actual new local authority with owned SQLite Identity/management/October, real existing hasher/managers and controlled credential source/clock. Cover valid member/standalone, wrong password, required factor, lockout/deletion/stamp/role/membership change, caller subject substitution, unknown target, cancelled/unavailable authority and cross-authority colliding IDs. Prove fresh reads rather than tracked/cached entities. Same-authority text changes preserve historical binding. RED is for changed production authority behavior, never missing symbols or already-correct 18.1. Exercise the actual console source through its accepted terminal overload with controlled keys/availability and captured output: successful credentials, optional factor, no secret echo, unavailable input and cancellation while awaiting keys. Keep parameterless redirected-console rejection as regression. Inspect the thin system-console binding separately for real availability, intercept and cancellable waiting; controlled tests are not native-terminal runtime proof. Replacing the whole credential source tests authentication consumers only, and cannot establish the concrete console behavior.
- **ID-T02**: Actual generator prepare/select must run with no Web process/listener. Use authorized runner-owned PostgreSQL A/B with valid co-located Identity/management records and test optional separate Identity explicitly. From no October schema/batch, prepare authorizes before migration and creates atomically; denied authority leaves October unmigrated. Start Web at A, restart at B, test mismatch/missing selection/no cross-target data access/no October migration on select/no reload switch. Actual WebApplicationFactory final ConfigureAppConfiguration proves one consistent immutable selection, all domain/Identity/context-factory connections, ordinary absence, rejected obsolete online inputs and absence of four old authority routes. Resource-gate external process/PostgreSQL checks; no batch TLS qualification remains.
- **ID-T03**: Verify complete distinct/deliberately shared provider profiles, incomplete/conflicting inputs and protected-secret failure. No secret/source diagnostic, paid call, failover or field mixing. Operator credentials and provider secrets are distinct purposes.
- **ID-T04**: Exercise production October direct verifier through real 18.1 issuer and existing root binding. Issue/revoke exact approvals; deny foreign tuple/authority/session/root, expired/revoked permissions before issuance, between calls and before disclosure. Make DB authority unavailable. Assert no payload/continuation/reactivation, correct cancellation and caller disposal. Reuse containment/schema tests instead of duplicating 18.1 behavior.

### Alternatives

- **ID-X01**: Reject schema-per-batch/search-path switching; fixed schema-qualified database-per-batch remains.
- **ID-X02**: Reject ordinary context/legacy Knowledge reuse for October rows, including when databases are co-located.
- **ID-X03**: Reject implicit configuration fallback or protected-row copying; require explicit complete provider profiles.
- **ID-X04**: Reject mandatory online Web bootstrap/authority; direct current database authority permits independent execution. Offline cached allow or test stub cannot replace it.
- **ID-X05**: Reject premature top-level/immediate configuration and ConfigureContainer without explicit factory; retain actual final-configuration Program proof.
- **ID-X06**: Reject caller context, flattened provenance, time-varying semantic digest and cross-authority ID aliasing.
- **ID-X07**: Reject database-login-to-user mapping as default: shared/pool/admin credentials collapse operator identity and SET ROLE affects current_user. Reject relocating or duplicating Identity. Reuse its owning assembly without executing the Web host; disclose credential/packaging consequences for approval.

### Consequences

- **ID-C01**: Operators select exact connections/batch at startup and restart to switch. Batch needs current authority databases but no Web process; independent execution is not cached offline authorization.
- **ID-C02**: Valid real Identity/management data remains required. All three schemas are co-located by default; only Identity may explicitly use a separate connection, while management follows the selected dataset. Prepare creates October setup, not a user-management product, and never assumes permission to seed/export/import real accounts or records.
- **ID-C03**: Story 18.1 at 3632b2cc via f190f8dc remains dependency evidence; new direct adapter/host composition needs its own qualification.
- **ID-C04**: Generator packaging includes the existing Web assembly solely for headless Identity reuse. No listener/entrypoint runs. Explicit operator credentials/factors and actual database access require authorized inputs/resources; removed transport requires no client certificate.
- **ID-C05**: Deferred final configuration/provider validation remains necessary. Test explicit authority connection selection through actual Program. Browser access remains independently authorized.
- **ID-C06**: Preserve completed work and historical approvals. Reconcile affected tasks only after plan acceptance; old completion marks cannot claim changed behavior is verified. Renew approval for headless authentication, connection layout and assembly dependency.

## Integrated Source Boundary and Review

The [18.1 contract](../016-contained-source-tools/contracts/prepared-source-tools.md), [Core types](../../src/dotnet/Vulgata.Core/DomainServices/PreparedSourceContracts.cs), [issuer](../../src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs), [functions](../../src/dotnet/Vulgata.Agents/WorkerExecution/PreparedSourceFunctions.cs) and [final verification](../016-contained-source-tools/evidence/final-verification-01.json) remain immutable. Current spec FR-011 requires current authority, not Web transport. No spec rewrite is necessary for this deployment correction. Context lifetime, cardinality, revocation and function surface remain unchanged.

## Requirement and Success Coverage

| Requirement | Design coverage |
| --- | --- |
| FR-001 | R01, I01, I05, I06, S02, T01 |
| FR-002 | R01, I01, I04, I05, S02, T01, T04 |
| FR-003 | S02, L01, T01 |
| FR-004 | R01, S02, S03, T01 |
| FR-005 | I02, I06, F01, T02 |
| FR-006 | S01, S03, T02 |
| FR-007 | R03, I03, T03 |
| FR-008 | I03, I05, F01, T03 |
| FR-009 | R02, S03, L02, T02 |
| FR-010 | I01, F01, T03 |
| FR-011 | I04, I05, S04, A01, L03, T04 |
| FR-012 | R01, I02, L02, T02 |
| SC-001 | T01, T02, T04 |
| SC-002 | T02 |
| SC-003 | T01, T03 |
| SC-004 | S03, T02 |
| SC-005 | F01, T03 |
| SC-006 | I04, I05, S04, T04; current review |

## Planned-Change Trigger Assessment

Fresh classification is architecture: authority source, headless interface, host assembly dependency and connection composition change. Independent plan review and applicable renewed authorization are required. No Worker function, Scan strategy or PRD requirement is added/weakened, so the three AGENTS-specific triggers are not established by this candidate. Root/reviewer must reassess and bind approvals to accepted revised scope; prior online-transport approval is not advance approval for this replacement. The user's correction authorizes planning, not resource use.

## Project Structure and Complexity

Existing mapping/projects remain. Core retains contracts/policy; Infrastructure retains October storage/selector/source adapters; Web/Data supplies headless Identity authority; generator references it and remains prepare/select only. No new project/service/package or database-login delegation system. The explicit assembly reuse avoids moving/duplicating Identity ownership; no constitution waiver is claimed.
