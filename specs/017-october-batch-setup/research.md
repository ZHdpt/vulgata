# Research: Owned October Batch Setup

These are static planning conclusions for the independent batch and the user's 2026-10-02 ordinary-Web correction. No runtime, migration, provider or new resource use was performed. Previous reviews/implementation and failed diagnostics remain historical evidence for their recorded boundaries.

## Deployment and database selection

**Decision**: Batch prepare/select executes directly with PostgreSQL and no running Web, URL, activation grant or certificate. Ordinary Web chooses only its existing DefaultConnection at startup and requires restart to change it. Web has no October mode, batch identity, setup-operator authentication or generator provider prerequisite.

**Rationale**: The explicit ordinary-Web decision supersedes the accepted special selected-data path. Corrected spec FR-005/006/007/009/012 and root-reconciled O-FR17 preserve exact generator batch identity while removing that Web startup coupling. FR-011 still requires live source authority, checked directly by the unchanged generator/source adapter.

**Alternatives**: Keeping both ordinary and special selected-data Web would retain an unrequested deployment path and the batch/operator/provider prerequisites the user removed. Bypassing only the failed credential check would leave the design mismatch. Inferring a latest batch would invent a read-selection contract. Remove special composition entirely and preserve ordinary behavior. The older authorization-only online bootstrap is also superseded; direct generator authority keeps live checks without a Web process or cached allow.

**Decision**: Generator DefaultConnection selects management/October data, with co-located Identity by default and its existing explicit separate Identity option. Ordinary Web uses DefaultConnection for both Identity and domain data; October configuration cannot redirect either. No special Web Identity override, central management server or implicit record copy is added.

**Rationale**: Existing ordinary Web composition uses DefaultConnection for Identity/domain with separate schemas/history. The new decision restores that path and its migrations/seeding, while pinning the connection through all scopes/factories for the process lifetime. October's separate context/model/history remains generator-owned; Web neither registers it nor validates a startup batch. Generator exact target/batch and authority identity still must agree.

**Alternatives**: A mandatory separate authority database adds an unrequested deployment requirement. The existing separate Identity option remains a generator option, not a new ordinary Web requirement. Dynamic schema/model switching and cross-database fallback remain rejected.

## Real operator authentication without a Web process

**Decision**: Reuse existing Identity types/managers/options/bcrypt in a small headless module physically in Web/Data, referenced by the generator assembly without executing Web Program. Authenticate username/password and required existing second factor through explicit protected-file or no-echo console input. Preserve Identity eligibility, lockout and factor semantics. Derive real subject from the authenticated account; no fabricated principal, account, role or DB-login mapping.

**Evidence**: ApplicationDbContext and ApplicationUser remain in Web/Data by Constitution I. Program registers IdentityOptionsConfiguration, UserManager/SignInManager, existing token providers and BcryptPasswordHasher. Login.razor uses PasswordSignInAsync with lockoutOnFailure:false and the existing factor flow. The accepted implemented generator-to-Web assembly reuse and headless registration remain; removal of special Web startup does not remove these generator dependencies. Current group evidence qualifies only its named authentication/source cases, not revised ordinary Web behavior.

**Counterexamples and alternatives**: A request/CLI SubjectId has no proof. A shared or pooled PostgreSQL login cannot distinguish real operators; current_user can change through SET ROLE, and a dedicated session_user mapping would create a new workload-delegation policy. Reimplementing Identity tables/hashers in Infrastructure duplicates ownership; relocating Identity broadens scope. A Web URL preserves the wrong deployment dependency. Existing Identity loaded in-process supplies reusable authentication without these alternatives.

**Consequences**: Explicit credential/factor input and required protection compatibility remain generator startup prerequisites; batch independence does not imply anonymous operation. Unsupported Identity mechanisms fail closed. Operator credential material is not persisted in October or mixed with provider credentials. Ordinary Web has no setup operator and retains its own browser authorization. Generator packaging includes Web's assembly but executes no listener, UI startup, role seeding or background Scan registration.

## Legitimate initial data and owned resources

**Decision**: Prepare adds October setup to an already authorized database with valid Identity/management records, possibly with optional separate Identity. It does not create users/roles, import or export real data, or migrate authority schemas. Spec's existing-context assumption remains. Missing records/schema/authentication fails before October migration.

**Rationale**: O-FR18 requires real operator and valid setup, and forbids fabricated Administrator/pair/ScanRun. The existing management/Identity owners provide legitimate records; requiring those records is an input condition, not a new prerequisite story or running Web requirement. No current instruction grants actual database/credential/export permission.

**Qualification route**: Owned test fixtures create real test Identity users through UserManager and assigned roles through existing supported APIs, with valid System/Repository/context rules, before first October prepare. Target A/B have no October schema at that point. Actual production input preparation requires its existing explicit authority; fixture setup must not become fallback production seeding.

## Context provenance and authority continuity

**Decision**: Keep the existing trusted OctoberAuthorizedSelection.SelectedContext, canonical v2 digest, exact applicability/difference rules and writer-owned first-commit SelectedAtUtc. The direct management adapter replaces the grant payload as the provenance source; caller requests still carry references only. No new Core reader or second writer.

**Rationale**: Prior I-18.4-CONTEXT-PROVENANCE-SEAM-01 showed references alone cannot preserve values. P-18.4-05 showed colliding record IDs cannot establish authority continuity. Both remain resolved: fresh current AuthorityId must equal persisted ManagementAuthorityId before any historical text/capability; exact ordinal comparison and explicitly pinned connection layout prevent implicit rebinding.

**Retry/read distinction**: Prepare compares complete semantic content and conflicts on a change; identical retry returns original time. Read-only selection checks current identity/membership/root/reference access and returns stored historical values/digest/time. Same-authority text edits do not overwrite history. No grant timestamp or expiry enters content identity.

**Alternatives**: Flattening context loses origin; caller text is forgeable; refreshed timestamps break idempotency; copying current text onto historical selection destroys provenance. All remain rejected.

## Direct source authority and lifetime

**Decision**: Keep the October source-approval rows, exact seven-field PreparedSourceApproval, binding cardinality and generator physical-root map. IOctoberLocalAuthority issues/revokes/verifies approval directly with fresh Identity/management/October scopes. Existing OctoberPreparedSourceAuthorityVerifier consumes that current decision plus current binding check, then unchanged PreparedSourceContextIssuer enforces source reads.

**Rationale**: Story 18.1 contract/issuer explicitly allow a later production authority source but require a mandatory verifier at issuance, during work and before disclosure. It does not prescribe HTTP. Every check reads current account/security stamp/eligibility/role/membership/approval/expiry/root version; startup authentication or database availability alone cannot authorize.

**Consequences**: Current DB authority must remain available even with Web stopped. No positive cache or long-lived tracked context. A lost/revoked session or unavailable database closes effective source authority; existing 18.1 context latches closure, discards payload and invalidates continuations. Separate Identity does not create distributed atomicity. Timeouts/cancellation settle owned work and preserve closed outcomes.

**Reuse**: PreparedSourceBinding, issuer, context and three functions remain unchanged. One context per Repository/approval; trusted caller owns async disposal after borrowed calls settle. No Worker dispatcher, fourth function, JDT-LS, CodeGraph or source-copy semantics are added.

**Caller reconciliation**: Actual OctoberGeneratorModule.ExecuteAsync authenticates and disposes its session at prepare/select completion; it performs no source work. The existing source-adapter integration harness constructs the session-capturing callback, verifier, issuer and binding and owns their lifetime. This is real production-adapter integration, not an executable generation caller. Story 18.4 acceptance and spec Assumptions reserve generation for later stories, while FR-011 still requires the callable current-authority adaptation and its integration proof. Retain those obligations and their completed T014/T015 evidence separately from host proof. Reject an unused factory, dummy source probe, retained setup session or new Worker hook. The shared contract preserves the existing construction and context-before-session disposal order; the ordinary-Web correction does not reopen that resolved design or change any task in this phase.

## Host ordering and regression

**Decision**: Keep the existing ConfigureServices(WebApplicationBuilder)->FrameworkCatalog interface; move ordinary database snapshot setup behind it and remove ConfigureOctoberHost. An explicit DefaultServiceProviderFactory and deferred ConfigureContainer capture DefaultConnection once before the provider is built. ApplicationDbContext, VulgataDbContext and IDbContextFactory<VulgataDbContext> consume that same internal immutable value. Keep ordinary migrations, role seeding, routes, browser Identity and provider administration; no new public selector is needed.

**Evidence**: Current Program.cs calls ConfigureServices then ConfigureOctoberHost. The latter selects October mode and registers the authority/binder; after Build the selected-data branch loads providers and authenticates setup, while the ordinary branch runs migrations/seeding. GetContextConnection currently rereads IConfiguration for ordinary scopes, whereas selected-data uses immutable authority configuration. Deleting selected-data alone would leave new ordinary scopes vulnerable to configuration reload. RetainedSourceCaptureApplicationFixture also calls ConfigureServices directly; retaining that interface keeps one composition owner. Prior plan-review-09 and the callable-seam lesson require actual WebApplicationFactory final-configuration proof, not selector-only tests.

**Alternatives and testability**: Early capture misses deferred test/host overrides; lazy singleton capture on first context can observe reload after Build. Replacing context registrations in the final callback could defeat legitimate ConfigureTestServices SQLite substitutions. Capture only the shared value at Build, retain existing ordinary option registrations and have their provider callbacks consume it. Test observable context/factory connection options and ordinary operations through actual Program; private snapshot shape remains local. No temporary service provider or test-only endpoint is introduced.

**Migration rule**: Generator prepare runs October migrations only after authentication, target/organization authorization and connected identity checks; it never migrates authority. Select requires an existing batch and does not migrate. Ordinary Web always retains its Identity/domain migrations and role seeding, even without an October batch, and never registers/migrates October. Owned fixtures pin domain history to public.__EFMigrationsHistory and Identity to identity.__IdentityMigrationsHistory, retaining the PostgreSQL history lesson. No schema or migration artifact changes follow from removing the startup branch.

## Provider and verification

**Decision**: Preserve generator host-supplied complete profiles, separate roles, deliberate sharing, no silent field mixing/failover or paid health call. Keep the implemented OctoberProviderBinder interface and existing encryption purpose. Generator selects one complete JSON document and binds before setup side effects. Remove the Web copy of document loading/binding and retained October result; ordinary provider administration and encryption consumers remain unchanged. No encrypted-row copy or additional secret resource is authorized.

**Rationale and alternatives**: T023 previously named no callable construction/result and would force the test author to invent it. A small concrete binder exposes validation depth without another resolver interface. Loading one declared document preserves source provenance; assembling profile fields from flattened multi-provider IConfiguration could silently mix sources. A result containing only validated metadata/references proves binding without creating a generation client or retaining plaintext. Existing encryption supplies protection compatibility; a new vault, purpose, copied provider registry or paid health probe would change accepted scope. Concrete input names/selection mechanics are explicitly left to planning by the spec.

**Verification**: Changed ordinary Web startup/database-lifetime behavior requires honest RED/GREEN through actual Program. Existing correct generator policy/digest/store/source/provider behavior uses retained/current regression; deleting unused glue or preserving behavior uses direct/refactor checks. Actual owned PostgreSQL/process runs demonstrate prepare/select with Web absent and ordinary Web A/B restart behavior, including no October inputs/batch, ordinary migrations/seeding, browser authorization and unchanged provider administration. Current testing.md keeps controlled, PostgreSQL and actual-process evidence separate. Prior special-Web CredentialUnavailable/log-lock diagnostics remain failed historical evidence; the new design is not proof they passed and grants no rerun. Independent plan acceptance and fresh scoped authorization precede tasks.
