# Research: Owned October Batch Setup

These are static planning conclusions for the user's independent-batch correction. No runtime, migration, provider or new resource use was performed. Previous reviews/implementation remain historical evidence for their recorded boundaries.

## Deployment and database selection

**Decision**: Batch prepare/select executes directly with PostgreSQL and no running Web, URL, activation grant or certificate. Web chooses its database and exact batch at startup and requires restart to change them.

**Rationale**: The user's independent-batch direction resolves the deployment mismatch. O-FR17 requires database-per-batch and startup-only selection; it does not require an online Web authority. Spec FR-011 requires current source authority, which can be checked directly. No spec intent contradicts this correction.

**Alternatives**: The earlier Web authorization-only bootstrap solved first-batch circularity by adding an online dependency; that solution is superseded by the user's arrangement. An offline cached allow would violate source currentness. A direct database authority keeps current checks without a Web process.

**Decision**: Default selected database contains unchanged identity/vulgata schemas plus isolated october. ConnectionStrings:DefaultConnection selects management and October data for each host. Identity defaults to that same connection; only an explicitly declared optional Identity connection may differ. No central management database is mandatory, and management never silently follows a previously selected database.

**Rationale**: Existing Web Program registers Identity and domain contexts from DefaultConnection, with separate schemas/history. The requested arrangement should let a Web configuration change select the dataset it serves. October's separate context/model/history remains required even if physically co-located. Exact target/batch and authority identity still must agree.

**Alternatives**: Mandatory separate authority database adds a deployment requirement the user did not ask for. Separating only Identity is an explicit option for an existing deployment; it is not implicit cross-database fallback. Dynamic schema/model switching remains rejected.

## Real operator authentication without a Web process

**Decision**: Reuse existing Identity types/managers/options/bcrypt in a small headless module physically in Web/Data, referenced by the generator assembly without executing Web Program. Authenticate username/password and required existing second factor through explicit protected-file or no-echo console input. Preserve Identity eligibility, lockout and factor semantics. Derive real subject from the authenticated account; no fabricated principal, account, role or DB-login mapping.

**Evidence**: ApplicationDbContext and ApplicationUser remain in Web/Data by Constitution I. Program registers IdentityOptionsConfiguration, UserManager/SignInManager, existing token providers and BcryptPasswordHasher. Login.razor uses PasswordSignInAsync with lockoutOnFailure:false and redirects required factor to the existing authenticator/recovery-code path. The generator currently references Core/Infrastructure only. It can reference the Web assembly without invoking its entrypoint; no reverse generator reference exists. The new assembly reference and headless registration are architecture decisions requiring review/approval and production composition tests, not a claim of current execution.

**Counterexamples and alternatives**: A request/CLI SubjectId has no proof. A shared or pooled PostgreSQL login cannot distinguish real operators; current_user can change through SET ROLE, and a dedicated session_user mapping would create a new workload-delegation policy. Reimplementing Identity tables/hashers in Infrastructure duplicates ownership; relocating Identity broadens scope. A Web URL preserves the wrong deployment dependency. Existing Identity loaded in-process supplies reusable authentication without these alternatives.

**Consequences**: Explicit credential/factor input and required protection compatibility become startup prerequisites; batch independence does not imply anonymous/unattended operation. Unsupported Identity mechanisms fail closed rather than bypassing a factor. Operator credential material is not persisted in October or mixed with provider credentials. Browser users never inherit the startup operator. Packaging includes Web's assembly and dependencies but no HTTP listener, UI startup, role seeding or background Scan registration.

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

## Host ordering and regression

**Decision**: Retain explicit DefaultServiceProviderFactory plus deferred ConfigureContainer and one immutable final configuration selection. In selected-data mode ensure all actual domain/Identity DbContext options and factories use the declared final connections, not early ordinary defaults. Ordinary mode keeps its existing registration/migration/seeding/routes.

**Evidence**: Previous bootstrap configuration failures and plan-review-09 established the need for real WebApplicationFactory Program-host evidence. Static framework source or selector-only tests cannot prove actual host composition. The correction changes route availability and authority source but retains that ordering lesson.

**Migration rule**: Prepare runs October migrations only after successful authentication, target/organization authorization and connected-target identity checks. Co-location permits authority reads in the selected database before October migration. Select/Web require existing schema/batch and do not migrate October; selected-data validates authority schema rather than implicitly migrating/seeding it. Ordinary Web startup retains its baseline migration behavior.

## Provider and verification

**Decision**: Preserve explicit host-supplied complete provider profiles, separate roles, deliberate sharing, no silent field mixing/failover or paid health call. Existing provider contract is unchanged. No encrypted-row copy or additional secret resource is authorized.

**Verification**: Changed authentication and independent-host behavior require honest RED/GREEN through actual production seams. Existing correct policy/digest/store/source behavior remains regression/test-only coverage. Public callable constructions/results/lifetimes/cancellation are pinned before tasks, following the project lesson. Actual owned PostgreSQL/process runs must demonstrate prepare/select with Web absent and Web A/B restart selection. Remove TLS qualification from superseded transport scope, retain all database/process/resource permissions and current testing.md suite routing.
