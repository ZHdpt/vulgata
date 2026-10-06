---
governed_delivery_classification: architecture
governed_delivery_reason: Reconciles ordinary Web database-only startup with independent generator authority and isolated persistence, changing the accepted host composition and O-FR17 startup requirement.
---

# Implementation Plan: Owned October Batch Setup

**Branch**: `oct14/story-18-4` | **Feature**: `specs/017-october-batch-setup` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Corrected Story 18.4 specification, integrated Story 18.1, the user's [independent batch direction](evidence/independent-batch-direction-01.json) and explicit [ordinary Web decision](evidence/ordinary-web-user-decision-01.json). This bounded correction removes special Web startup composition; historical approvals and implementation remain evidence for their original boundaries. Independent plan review and fresh scoped authorization precede task reconciliation; the decision in principle is not advance approval of this revised plan.

## Summary

The batch executable runs independently. It connects directly to explicitly configured PostgreSQL authority and batch databases, authenticates a real operator through existing Identity loaded in-process, and uses shared setup/storage rules. It requires no running Web process, Web URL, activation grant, client certificate or HTTP authority service. Web starts ordinarily with only `ConnectionStrings:DefaultConnection` changed and snapshots that database choice until restart. Web neither selects a batch nor authenticates a setup operator at startup.

The generator's default selected database contains separate `identity`, `vulgata` and `october` schemas with unchanged ownership; its optional explicitly separate Identity database remains supported. Ordinary Web uses its existing DefaultConnection for both Identity and domain contexts/factories, applies their ordinary migrations and seeds roles, and never registers or migrates October persistence. A database need not contain an October batch for Web to start. No target/batch metadata, October mode, setup credentials or generator provider document is required by Web; no records or credentials are copied implicitly.

Generator headless authentication reuses existing Identity password, eligibility and required second-factor rules. Credentials come from an explicit protected source or no-echo operator input, never command-line arguments. Browser users authenticate through ordinary Web Identity and receive only their own current permissions. Normal provider administration remains unchanged; generator provider configuration never gates Web startup. This removes a startup coupling, not source authorization or later Knowledge read selection.

## Technical Context

**Stack**: C#/.NET 10, EF Core 10.0.9, Npgsql EF provider 10.0.3, existing ASP.NET Core Identity/Data Protection. No new package or authentication product.

**Storage**: PostgreSQL 17; one owned database per batch, identical fixed `october` schema, `october.__OctoberMigrationsHistory`, separate migrations/snapshot. Existing `identity`/`vulgata` mappings and histories remain unchanged. Co-location does not merge models.

**Hosts**: Existing `Vulgata.OctoberGenerator` with bounded `prepare`/`select`; ordinary Web only. The previous `selected-data` and `authorization-only` modes, October startup binder and four October grant/approval HTTP routes are superseded. Analysis, Worker dispatch, JDT-LS, CodeGraph and Knowledge generation remain excluded.

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
- **ID-R02**: Generator consumes the shared direct authority, policy, writer and selector. Prepare verifies connected identity and reads existing Identity/management authority before any October migration, then migrates only October and creates the explicitly selected first batch without Web. Authority reads may open the same database because default schemas are co-located. Select requires an existing exact batch and never migrates. Web consumes its ordinary Identity/domain registrations and freezes only its final database choice; ordinary migrations, role seeding, browser authorization and provider administration remain. Web has no October operational registration, startup authority, batch binding or provider binder.
- **ID-R03**: The generator-local binder resolves complete Worker/Chat profiles from one explicitly declared source with visible precedence; agents never choose credentials/providers. Ordinary Web continues its existing provider registry/administration and does not invoke this binder at startup.

### Interfaces

- **ID-I01**: Keep `OctoberBatchSetupPolicy.PrepareAsync(OctoberSetupRequest, OctoberAuthorizedSelection, CancellationToken) -> Task<OctoberPrepareResult>`, writer `CommitAsync` and existing Core records unchanged. Only Created/AlreadyIdentical return exact committed identity and first-commit UTC time; failures carry neither identity nor capability. The trusted adapter supplies subject/canonical database/context. Request input carries references only; canonical v2 `OctoberSelectionDigest.Compute` remains shared.
- **ID-I02**: Keep `IOctoberBatchSelector.BindAsync(OctoberStartupSelection, CancellationToken) -> Task<OctoberSelectionResult>` and historical snapshot capability. Fresh direct authority replaces Web validation. Before historical text/capability exposure, require stored/current ManagementAuthorityId, subject, target/batch and stable organization/root/reference equality. Same-authority advisory text edits do not overwrite history and need not match historical digest for read-only selection; prepare retry requires the full semantic identity. Generator command/module and connected-target interfaces are fixed in [the shared contract](contracts/batch-setup-selection.md).
- **ID-I03**: Generator provider source/precedence/API type/endpoint/model/credential reference and role profiles are explicit. Generator loads one explicitly named complete host-supplied provider document and calls OctoberProviderBinder(ISecretEncryptionService).BindAsync with its immutable OctoberProviderConfiguration before October setup side effects. Preserve the implemented binder's exact inputs, closed result, existing LlmApiKey purpose and lifetime. Validate both roles atomically; no field borrowing, undisclosed environment discovery, encrypted-row copy or decrypted key in the result. Remove Web's document loader, binder invocation and retained October provider snapshot; normal provider administration is unaffected. See [provider contract](contracts/provider-binding.md).
- **ID-I04**: Keep one durable binding per (BatchId, RepositoryId), opaque RootBindingIdentity version and descriptive label. Only deployment configuration maps exact target/batch/Repository/version to a physical directory. The direct authority issues the exact approval after current checks; existing PreparedSourceBinding owns root lookup and calls unchanged PreparedSourceContextIssuer with unchanged PreparedSourceGrantRequest. At this setup-only boundary the production deliverable is these callable adapters, qualified together by controlled integration as specified in the shared contract. Neither prepare/select nor Web startup issues a source context; no production generation caller is claimed or added. No durable paths/contexts/continuations or caller-selected roots.
- **ID-I05**: The shared `IOctoberLocalAuthority` and its `OctoberLocalAuthority` Web/Data adapter replace Web transport. The shared contract pins constructor dependencies, opaque authenticated session, AuthenticateAsync, AuthorizeSelectionAsync, IssueSourceApprovalAsync, VerifySourceApprovalAsync and RevokeSourceApprovalAsync. Reuse existing Identity hash/sign-in/second-factor semantics; derive subject from the authenticated account. Fresh scopes recheck security stamp, eligibility/lockout, roles, System ownership, Repository membership, exact target and approval. No database-login-to-user mapping, request principal or cached permission is introduced. Preserve `OctoberConsoleCredentialSource()` for real-console composition and add only `OctoberConsoleCredentialSource(IOctoberCredentialTerminal terminal)` for the same concrete source. The contract fixes terminal availability, cancellable key input with explicit intercept/no-echo choice, prompt output, ownership and failures; it does not change credential/authentication policy or authorize a real terminal.
- **ID-I06**: Retain the existing public `VulgataWebComposition.ConfigureServices(WebApplicationBuilder builder) -> FrameworkCatalog` interface and actual `Program` host as the test seam. Behind ConfigureServices, register ordinary services once and use explicit DefaultServiceProviderFactory plus deferred ConfigureContainer<IServiceCollection> to capture final `ConnectionStrings:DefaultConnection` once before provider construction. An internal immutable value, not a new public selector, supplies the same connection to ApplicationDbContext, VulgataDbContext and IDbContextFactory<VulgataDbContext>; later scopes/factories never reread live configuration. Preserve development validation flags and supported test service substitutions. Remove ConfigureOctoberHost and the selected-data startup selector/binder/bound-setup branch. October settings have no Web startup effect; no setup authentication, October registration, batch query or provider binding occurs. Ordinary migrations/role seeding execute before serving. No temporary provider, injected test selection or new endpoint. The shared contract pins construction, failures, lifecycle and observable tests.

### State and Lifetime

- **ID-S01**: Generator connections, authority identity, exact batch and provider profiles are fixed until restart. Ordinary Web's DefaultConnection snapshot is fixed before Build and used by every later Identity/domain scope and factory; reload never retargets it. This freezes database selection only, not ordinary provider administration. Authorization remains live. Restart cannot rebind an existing batch to a different authority; root mapping changes require a new version. Web's database choice grants no batch/source/read authority.
- **ID-S02**: One transaction commits batch/repository/root/reference rows, three applicability slots per Repository, differences, ManagementAuthorityId and writer-owned SelectedAtUtc. Preserve exact values/origins and AdvisoryNotSourceProof; null differs from empty. Identical retry returns original time; management edits never rewrite selected history. Separate authority reads are not a distributed atomic transaction, including when optional Identity is on another database; protected use rechecks live authority.
- **ID-S03**: Keep separate October model/history/snapshot and configuration filtering. Ordinary Web neither registers nor migrates October, including against a database containing October rows; it retains Identity/domain migrations and role seeding. Generator prepare never creates a database, fabricates an Administrator, copies users/secrets or migrates/seeds authority. Valid Identity/management records are established through existing supported owners independently of Web execution. Co-location requires these records already to exist; a fresh October schema is not an empty Identity authority.
- **ID-S04**: Short-lived source approvals store exact tuple/expiry/revocation in October. Generator ExecuteAsync owns its authentication session only through awaited prepare/select and disposes it before returning; returned committed identity/bound setup is metadata, not live source authority. Ordinary Web creates no setup session. The controlled source-adapter integration caller authenticates its own session, obtains exact bound setup and approval, captures that same session in the verifier callback, and keeps it alive until all borrowed calls settle and every issued context is asynchronously disposed, then disposes the session. Each context binds one Repository/approval. Production generation lifetime belongs to its later owning story and cannot reuse a disposed setup session. No session, scoped DB context, handle or continuation is persisted/reused after restart; reissue requires fresh current authority and is never automatic after closure.

### Async and Cancellation

- **ID-A01**: All async seams accept cancellation and bounded deadlines; fresh scopes/EF commands are bounded. Identity methods without cancellation are awaited with the scope alive and checked before/after; no success after cancellation and no abandoned task on disposed services. Source verification respects the incoming remaining deadline. Cancellation before commit rolls back, acknowledged commit preserves success, uncertain acknowledgement uses exact replay. Story 18.1 retains its own cancellation/disposal/continuation rules unchanged.

### Failure and Retry

- **ID-F01**: Closed sanitized failures distinguish invalid configuration/selection, unauthenticated/ineligible operator, required-factor failure, denied target/membership/root, unavailable authority, absent/mismatched batch, provider/storage failure and cancellation. No alternate database/latest batch/Web grant/legacy fallback. Only exact live tuple/root returns Current; lost identity/stamp/role/membership, revoked/expired approval denies; database outage returns Unavailable and 18.1 closes the context. Preserve all existing issue statuses and empty failure payloads. Never log credentials/factors/stamps/connection secrets/context/source/physical roots.
- **ID-F02**: Identical setup replay yields AlreadyIdentical/original time; changed semantic content yields Conflict without overwrite. No automatic authentication/factor/cross-database/provider retry. Failed authentication supplies no session; explicit fresh authentication cannot resurrect a closed source context.

### Reuse and Locality

- **ID-L01**: Reuse ApplicationDbContext/ApplicationUser, Identity options/bcrypt/managers in Web/Data through a small headless registration entrypoint, without request-scoped IUserContext or full Web registration. Reuse management roles/System-owner semantics; standalone Repository requires live ManagementAccess with no fake System. Preserve context origins rather than using flattened composition.
- **ID-L02**: Reuse policy/digest, October store/migrations, selector, root binding and the deferred configuration mechanism. Remove special-Web startup composition and its now-unused selector/binder/bound-setup types without removing the generator's headless Identity module or shared source adapters. Retired online grant/certificate routes and OctoberWebAuthorityClient stay absent. Preserve historical evidence and unrelated drafts; keep ordinary registration behind its existing interface. Generator never invokes Web Program or full application composition.
- **ID-L03**: Story 18.1 owns approval/grant/verifier/issuer/context and the closed three functions. Change only October's authority callback from HTTP to live DB checks; no copied source policy, schema, tool or dispatcher.

### Test Seams

- **ID-T01**: Retain policy/provenance/digest/writer regression tests. Exercise actual new local authority with owned SQLite Identity/management/October, real existing hasher/managers and controlled credential source/clock. Cover valid member/standalone, wrong password, required factor, lockout/deletion/stamp/role/membership change, caller subject substitution, unknown target, cancelled/unavailable authority and cross-authority colliding IDs. Prove fresh reads rather than tracked/cached entities. Same-authority text changes preserve historical binding. RED is for changed production authority behavior, never missing symbols or already-correct 18.1. Exercise the actual console source through its accepted terminal overload with controlled keys/availability and captured output: successful credentials, optional factor, no secret echo, unavailable input and cancellation while awaiting keys. Keep parameterless redirected-console rejection as regression. Inspect the thin system-console binding separately for real availability, intercept and cancellable waiting; controlled tests are not native-terminal runtime proof. Replacing the whole credential source tests authentication consumers only, and cannot establish the concrete console behavior.
- **ID-T02**: Actual generator prepare/select runs with Web stopped against owned PostgreSQL A/B; exercise co-located and generator-only separate Identity layouts. Prepare authorizes before October migration and commits atomically; denied authority leaves October unmigrated. Generator select rejects missing/mismatched batch and never migrates. Actual WebApplicationFactory<Program>.ConfigureAppConfiguration and real ordinary Web processes start with only DefaultConnection changed: no October inputs, no batch required, ordinary migrations/seeding/browser authorization/provider administration retained and zero October registration/migration. Verify final callback override, same Npgsql target in ApplicationDbContext/VulgataDbContext/domain factory, reload before first scope and after use cannot retarget, restart A to B, and no cross-target access. October settings cannot activate a special path; four old authority routes remain absent. Controlled SQLite substitutions are distinct from real PostgreSQL/process proof; no test-only route or synthetic selector proves startup. Pin domain history to public.__EFMigrationsHistory and Identity history to identity.__IdentityMigrationsHistory in owned fixtures. Resource-gate execution; no TLS qualification is added.
- **ID-T03**: Retain concrete provider binder coverage for complete distinct/deliberately shared profiles, incomplete/conflicting inputs, protected-secret failure and cancellation; atomic closed results expose no decrypted material. Use owned ephemeral protection keys for compatibility and controlled encryption for failures. Actual generator startup invokes this binder from one explicit complete document before setup side effects. Ordinary Web starts without that file/binder and retains normal provider administration. Verify generator immutable selection/no cross-source backfill and unchanged no-secret/source diagnostics, paid-call absence, no failover or field mixing. Operator credentials and provider secrets remain distinct purposes.
- **ID-T04**: Controlled integration exercises actual OctoberLocalAuthority issue/verify/revoke, OctoberPreparedSourceAuthorityVerifier and PreparedSourceBinding through the real 18.1 issuer; its test caller owns composition/session/context lifetime under the shared contract. Issue/revoke exact approvals; deny foreign tuple/authority/session/root, expired/revoked permissions before issuance, between calls and before disclosure. Make DB authority unavailable. Assert no payload/continuation/reactivation, correct cancellation, context disposal before session disposal, and denial when a disposed session is reused. This proves production adapters working together, not an executable generator source consumer. Separately retain actual module prepare/select session-disposal coverage under T02. Existing correct integration uses passing coverage/regression, not fabricated RED; changed production behavior requires behavioral RED/GREEN. Reuse containment/schema tests instead of duplicating 18.1 behavior.

### Alternatives

- **ID-X01**: Reject schema-per-batch/search-path switching; fixed schema-qualified database-per-batch remains.
- **ID-X02**: Reject ordinary context/legacy Knowledge reuse for October rows, including when databases are co-located.
- **ID-X03**: Reject implicit configuration fallback or protected-row copying; require explicit complete provider profiles.
- **ID-X04**: Reject mandatory online Web bootstrap/authority; direct current database authority permits independent execution. Offline cached allow or test stub cannot replace it.
- **ID-X05**: Choose removal of special selected-data Web startup over keeping a dual ordinary/special mode: the latter retains unrequested batch/operator/provider prerequisites and violates the explicit decision. Reject merely bypassing failed credentials, assuming a latest batch, or suppressing ordinary migrations to keep old tests passing. Also reject early configuration capture, lazy first-use snapshots and ConfigureContainer without an explicit factory; final deferred capture preserves normal configuration overrides and restart-only database selection.
- **ID-X06**: Reject caller context, flattened provenance, time-varying semantic digest and cross-authority ID aliasing.
- **ID-X07**: Reject database-login-to-user mapping as default: shared/pool/admin credentials collapse operator identity and SET ROLE affects current_user. Reject relocating or duplicating Identity. Reuse its owning assembly without executing the Web host; disclose credential/packaging consequences for approval.

### Consequences

- **ID-C01**: Generator operators select exact connections/batch and restart to switch. Web operators change only ordinary DefaultConnection before restart; no batch is selected by startup. Batch needs current authority databases but no Web process; independent execution is not cached offline authorization or later read-selection design.
- **ID-C02**: Generator requires valid real Identity/management data; its three schemas are co-located by default and only its Identity connection may explicitly differ. Prepare creates October setup, not a user-management product. Ordinary Web preserves its normal Identity/domain migrations and role seeding in DefaultConnection; no new October Identity override, account import/export or seed policy is introduced.
- **ID-C03**: Story 18.1 at 3632b2cc via f190f8dc remains dependency evidence; new direct adapter/host composition needs its own qualification.
- **ID-C04**: Generator packaging includes the existing Web assembly solely for headless Identity reuse. No listener/entrypoint runs. Explicit operator credentials/factors and actual database access require authorized inputs/resources; removed transport requires no client certificate.
- **ID-C05**: Deferred database capture remains necessary for ordinary Web; explicit provider validation remains generator-only. Existing special-Web deployment settings are no longer consumed. Browser access remains independently authorized and normal provider administration remains mutable through its existing authorized operations.
- **ID-C06**: Preserve completed work and historical approvals for their recorded scope. Special-Web GREEN and the exhausted diagnostic runs do not qualify ordinary startup or become passing evidence. Reconcile affected tasks only after independent plan acceptance and fresh scoped authorization for the O-FR17/host-composition change, then obtain affected definition review/implementation approval. Old completion marks alone cannot prove changed behavior.

## Integrated Source Boundary and Review

The [18.1 contract](../016-contained-source-tools/contracts/prepared-source-tools.md), [Core types](../../src/dotnet/Vulgata.Core/DomainServices/PreparedSourceContracts.cs), [issuer](../../src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs), [functions](../../src/dotnet/Vulgata.Agents/WorkerExecution/PreparedSourceFunctions.cs) and [final verification](../016-contained-source-tools/evidence/final-verification-01.json) remain immutable. Corrected spec FR-011 still requires current source authority. Its other revised requirements remove special Web startup, not source checks. Context lifetime, cardinality, revocation and function surface remain unchanged.

Retain FR-011's implemented approval/binding adapters and required real-adapter integration. The controlled caller owns its session/context lifetime, not a hidden executable generation caller. Existing completed T014/T015 work is not reopened merely by this Web correction. No unused public factory, ExecuteAsync callback, source probe or retained session is added. Actual current Identity descriptor checks remain inside the existing authority adapter; Web removal must not remove those generator/source checks.

The next authorized tasks phase reconciles T018-T020, T023-T026, T029-T030 and their dependencies for ordinary Web, preserving all IDs and unchanged completed work. Retain correct generator provider declarations/binder/wiring evidence without repeating scaffolding or RED. Changed Web startup and database-lifetime behavior require public-host behavioral RED/GREEN; deletion/refactor of unused glue uses appropriate regression/direct checks. Final ordinary Web A/B PostgreSQL/process qualification follows revised wiring and affected approvals. Old selected-data failures stay historical; no new runtime invocation is authorized by this plan. Tasks are unchanged in this phase.

## Requirement and Success Coverage

| Requirement | Design coverage |
| --- | --- |
| FR-001 | R01, R02, I01, I05, S02, T01, T02 |
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
| FR-012 | R01, R02, I02, I06, L02, T02 |
| SC-001 | T01, T02, T04 |
| SC-002 | T02 |
| SC-003 | T01, T03 |
| SC-004 | S03, T02 |
| SC-005 | F01, T03 |
| SC-006 | I04, I05, S04, T04; current review |

## Planned-Change Trigger Assessment

Fresh classification is architecture: this correction changes the accepted Web host composition, database lifetime and affected O-FR17 startup requirement, while retaining shared authority and isolated persistence. The human selected ordinary Web in principle and root reconciled the lifecycle authority; that instruction is not advance approval of this complete revised plan. Independent plan review and fresh scoped planned-change authorization are required before task reconciliation, followed by affected definition review and implementation approval. The requirement-change and architecture triggers apply; no new Worker function, Scan strategy, source-read authority or later-generation scope is proposed. Historical approvals retain only unchanged semantic scope. This author result grants no implementation, diagnostic retry, resource use, recovery extension or waiver.

## Project Structure and Complexity

Existing mapping/projects remain. Core retains contracts/policy; Infrastructure retains October storage/selector/source adapters; Web/Data supplies headless Identity authority; generator references it and remains prepare/select only. No new project/service/package or database-login delegation system. The explicit assembly reuse avoids moving/duplicating Identity ownership; no constitution waiver is claimed.
