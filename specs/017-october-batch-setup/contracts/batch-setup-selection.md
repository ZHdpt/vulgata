# Shared October Setup and Selection Contract

This is the revised Story 18.4 contract for an independently runnable batch and startup-configured Web. It replaces the former Web activation/grant/HTTP-verifier design. Story 18.1's source-access contracts are unchanged. Historical transport code/evidence is retained until affected implementation tasks reconcile it; it is not a supported fallback.

## Exact deployment configuration

Default October deployment uses one selected owned PostgreSQL database containing separate `identity`, `vulgata` and `october` schemas. The operator changes Web's `ConnectionStrings:DefaultConnection` plus exact target/batch metadata before startup; after restart all domain/October reads use the selected dataset. There is no runtime switch, latest-batch lookup or automatically retained central management server.

Both executable hosts accept these immutable startup inputs:

| Input | Meaning |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Explicit protected connection for the chosen batch database; existing management and October contexts use this exact database. |
| `October:TargetId`, `October:BatchId` | Exact opaque deployment target and nonempty Guid batch. Web equivalents are `October:Web:TargetId`, `October:Web:BatchId`; configuring conflicting variants is rejected. |
| `October:Targets` | Deployment-owned target allowlist with exact TargetId and CanonicalDatabaseIdentity. The selected connection must match its descriptor and connected PostgreSQL database identity before any migration. No request-provided connection. |
| `October:Authority:Id` | Nonblank, no surrounding whitespace, deployment-owned identity for the selected management/Identity authority. Exact ordinal comparison; must agree with persisted ManagementAuthorityId. |
| `October:Identity:ConnectionName` | Optional explicit alternative named connection for Identity only. Absent means DefaultConnection by the declared co-location rule. If present, that named connection must exist; no fallback. Management always remains on the selected batch database. |
| `October:Identity:CanonicalDatabaseIdentity` | Required only with separate Identity configuration, pins its intended database independently. Identity connection change requires restart and a matching authority identity. |
| `October:Operator:CredentialSource` | Exactly `console` or `protected-file`; no default or environment/secret discovery. |
| `October:Operator:CredentialFile` | Only for protected-file: explicit file containing a Data Protection payload for purpose `october-operator-credentials-v1`, readable through configured key ring/application identity. No plaintext password/factor in config, CLI, logs or diagnostics. |
| `October:PreparedRoots` | Generator-only exact target/batch/Repository/root-version to contained physical-directory mappings. Paths stay trusted host configuration. |
| Provider configuration | Complete separately declared profiles under the unchanged provider contract. No credential purpose mixing. |

Generator `October:Mode` is exactly prepare or select. Web `October:Web:Mode` is absent/ordinary or selected-data. Obsolete authorization-only, ActivationGrant, WebAuthorityEndpoint, ManagementAuthorityId aliases and GeneratorCertificates inputs fail explicit October configuration rather than activating old transport or silently substituting authority. Ordinary mode neither requires nor consumes October credentials, connections or bindings; existing DefaultConnection behavior for Identity/domain startup remains.

Selected-data Web uses the chosen DefaultConnection for domain and October contexts. Its Identity context uses the declared co-location rule or explicit alternative. Generator uses the same rules directly. Separate Identity is optional; it does not create a running-Web requirement or allow domain reads to follow an old database. The authority ID identifies the actual configured management/Identity combination and cannot be reused for a different authority merely because IDs collide. Connection descriptors include exact database name and deployment endpoint identity, exclude secrets from diagnostics, and reject alias ambiguity; connected `current_database()` must equal the selected descriptor. A reachable database is never ownership or user permission.

The operator authorizes/provisions the database and legitimate Identity/management inputs. Prepare assumes valid existing records as spec Assumptions state; it is not an account/role/system import product. Co-located targets may have Identity/vulgata schema and records but no October schema/batch. Existing owners' migrations/account-management/Repository/context setup are used under their own actual authority; no copied credential/hash, invented Administrator, seed catalogue or newly presumed export permission is supplied by this story. In qualification, run-owned fixtures use existing Identity UserManager account/role creation and valid domain rules with synthetic test credentials; this is test composition, never a production fallback. Missing schemas/real operator/valid records returns a sanitized prerequisite failure before October migration. No prerequisite story or online Web bootstrap is introduced.

## Headless Identity module and caller proof

Identity remains in Web/Data. Add a public `OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(IServiceCollection services, OctoberAuthorityConfiguration configuration, IOctoberOperatorCredentialSource credentialSource, TimeProvider clock) -> IServiceCollection` there. It registers only the existing ApplicationDbContext/ApplicationUser store, current IdentityOptionsConfiguration, bcrypt hasher, required existing Identity managers/token providers, fresh management context factory and October authority adapter. It never calls Web Program/ConfigureServices, starts a listener, seeds roles, migrates Identity/domain, or registers UI/controllers/background Scan work. Generator explicitly references the Web assembly to reuse this owner; its own entrypoint remains selected by its project. Web has no reverse generator reference.

`OctoberAuthorityConfiguration` is an immutable host-owned object containing AuthorityId, selected target ID/canonical database identity, batch ID, selected data connection, explicit Identity connection and identity descriptor, immutable allowed-target set, maximum authority-operation timeout (30 seconds, lowered by incoming deadline), and authentication-session lifetime (one hour maximum). It is never an HTTP/CLI DTO and its diagnostic representation suppresses connections. Connections are created through normal provider configuration, not model/request strings.

Its callable constructor is `OctoberAuthorityConfiguration(string authorityId, string targetId, Guid batchId, string canonicalDatabaseIdentity, string dataConnectionString, string identityConnectionString, string identityCanonicalDatabaseIdentity, ImmutableDictionary<string, string> allowedTargets, TimeSpan operationTimeout, TimeSpan sessionLifetime)`. It is a sealed immutable class with read-only properties of those names/types, suppressed diagnostic representation, copied immutable allowlist and fail-closed validation (nonempty exact identities; positive timeout at most 30 seconds; positive session lifetime at most one hour). Configuration resolution supplies the declared co-location connection explicitly to both properties; no downstream constructor infers fallback. Production composition snapshots settings once; controlled tests may construct the same configuration with owned database connections.

`IOctoberOperatorCredentialSource.ReadAsync(CancellationToken) -> ValueTask<OctoberOperatorCredentials?>` is Web/Data's host-input seam. `OctoberOperatorCredentials` is a sealed disposable class, not a record with generated secret-printing ToString; it carries username, password, and at most one optional authenticator code or recovery code. It exposes no subject/role claim. Console source uses no-echo input, only when an interactive terminal is available; redirected/noninteractive console mode fails visibly. Protected-file source decrypts only the explicitly named file with declared keys/application/purpose; missing/incompatible protection fails closed. Secrets are held only during authentication and released/disposed afterward; immutable framework password strings cannot be claimed securely zeroed. No source reread or automatic authentication retry occurs. Missing input is no permission.

The credential constructor is `OctoberOperatorCredentials(string userName, string password, string? authenticatorCode = null, string? recoveryCode = null)` with read-only properties, IDisposable, and rejection of simultaneous factor methods. Production sources are `OctoberConsoleCredentialSource()` and `OctoberProtectedFileCredentialSource(string path, IDataProtectionProvider protection)`, both implementing the same interface. Their failures return null for absent input, propagate caller cancellation, or throw sanitized input-unavailable failure mapped by AuthenticateAsync to Unavailable. The protected JSON payload has exactly userName/password/authenticatorCode/recoveryCode; unknown fields, both factors or invalid types are rejected. Creating that encrypted input is an operator-side use of the same existing Data Protection provider/purpose, not a new command, account or unapproved key-ring provisioning action. Tests inject controlled input through this interface while invoking the same concrete authentication adapter.

### Concrete console input seam

The parameterless console constructor remains the normal production entrypoint. Add one public overload and one terminal dependency in the existing `Vulgata.Web.Data` namespace; both constructors use the same credential-entry implementation:

```csharp
public interface IOctoberCredentialTerminal
{
    bool IsInteractive { get; }
    ValueTask<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken);
    void Write(string value);
}
// Constructors on the existing concrete credential source:
OctoberConsoleCredentialSource();
OctoberConsoleCredentialSource(IOctoberCredentialTerminal terminal);
```

The overload rejects null with ArgumentNullException. The injected terminal is borrowed, is never disposed by the source and conveys only input/output, never identity or authorization. The parameterless constructor supplies a thin internal system-console adapter, without a new public factory, host mode or service. Tests supply a controlled terminal to the same concrete source; no reflection/private-method testing or process-global Console.SetIn replacement is needed.

ReadAsync checks caller cancellation before accessing the terminal, then rejects noninteractive input with the existing static sanitized unavailable failure before consuming keys. The source requests every credential key with `intercept: true`; prompts/separators go through Write, and credential characters, password/factor values and exception details never do. Enter terminates a field, Backspace edits the current field without echo, and other non-text control keys do not become credential characters. Read username/password and optional authenticator/recovery fields without imposing a new authentication policy; empty optional fields become null, and simultaneous factors remain rejected. Missing required input yields no credentials. Cancellation while awaiting input or before returning a result propagates OperationCanceledException and returns no partially entered credentials. Terminal availability/read/write failures map to the existing sanitized unavailable failure; they never return partial success or raw console exception text. Transient entry buffers are released on every exit without claiming secure erasure of immutable strings.

The system adapter derives IsInteractive from actual input/output redirection and Environment.UserInteractive, uses the supplied intercept flag for native key reads, and supports cancellation while waiting for a key. It must not leave an uncancellable blocking Console.ReadKey running on an abandoned Task.Run after cancellation. A cancellable key-availability wait followed by an intercepted read is suitable when the adapter is the exclusive application console reader; serialize overlapping source reads with cancellable admission and do not introduce competing readers. The thin adapter owns no console handle and never closes process stdin/stdout. This describes its required behavior, not a prescribed private polling helper or timing constant.

Controlled tests assert returned credentials and captured output across the concrete source, with a terminal that models native echo when intercept is false so an accidental echo request is observable. Cover successful input, optional-factor handling, unavailable input, cancellation before/during entry, sanitized terminal failure and no partial/secret output. Preserve current parameterless redirected-console and protected-file passing evidence. Static inspection of the thin system binding separately checks actual availability, native intercept and non-abandoned cancellable waiting; it is not reported as real-terminal execution. This correction introduces no native-process/terminal permission or additional mandatory runtime qualification gate.

The alternative parameterless-only seam cannot exercise interactive success in a redirected SelfContained testhost: Console.SetIn does not supply native ReadKey events or interactive availability. Substituting IOctoberOperatorCredentialSource bypasses the behavior under test. The small terminal dependency is chosen over process-global mutation, reflection, a test-only credential algorithm or an unapproved real-terminal harness. Authentication, database/provider selection, ordinary startup and Story 18.1 remain unchanged.

`OctoberLocalAuthority` implements the following Core-facing interface. Core carries domain statuses and capabilities, never Identity/EF/connection/password types:

```csharp
public interface IOctoberOperatorSession : IAsyncDisposable
{
    string SubjectId { get; }
    string ManagementAuthorityId { get; }
    DateTimeOffset ExpiresAtUtc { get; }
}
public enum OctoberAuthenticationStatus
{
    Authenticated, Denied, AdditionalAuthenticationRequired, Unavailable, Cancelled
}
public sealed record OctoberAuthenticationResult(
    OctoberAuthenticationStatus Status, IOctoberOperatorSession? Session);
public sealed record OctoberAuthoritySelectionRequest(
    string TargetId, Guid BatchId, OctoberSetupRequest? PrepareRequest);
public enum OctoberAuthorityDecisionStatus
{
    Authorized, Denied, MissingBatch, BatchMismatch, Unavailable, Cancelled
}
public sealed record OctoberAuthoritySelectionResult(
    OctoberAuthorityDecisionStatus Status,
    OctoberSetupRequest? Request, OctoberAuthorizedSelection? Authority);
public enum OctoberSourceApprovalStatus
{
    Issued, Denied, Unavailable, Cancelled
}
public sealed record OctoberSourceApprovalResult(
    OctoberSourceApprovalStatus Status,
    PreparedSourceApproval? Approval, DateTimeOffset? ExpiresAtUtc);
public enum OctoberRevocationStatus
{
    Revoked, AlreadyRevoked, Denied, Unavailable, Cancelled
}
public interface IOctoberLocalAuthority
{
    Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken);
    Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(
        IOctoberOperatorSession session, OctoberAuthoritySelectionRequest request,
        CancellationToken cancellationToken);
    Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(
        IOctoberOperatorSession session, IOctoberHistoricalBoundSetupData boundSetup,
        Guid repositoryId, CancellationToken cancellationToken);
    Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(
        IOctoberOperatorSession session, string targetId,
        PreparedSourceApproval approval, CancellationToken cancellationToken);
    Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(
        IOctoberOperatorSession session, string targetId, string approvalIdentity,
        CancellationToken cancellationToken);
}
```

The concrete public constructor is `OctoberLocalAuthority(IServiceScopeFactory scopes, OctoberAuthorityConfiguration configuration, IOctoberOperatorCredentialSource credentials, TimeProvider clock)`. It retains factories/configuration, not scoped EF contexts or UserManager. Null dependencies throw ArgumentNullException; malformed runtime selections return closed denial. All calls use fresh owned async scopes disposed after awaited work. Concurrent verifier calls use distinct scopes; no DbContext is shared across operations.

AuthenticateAsync reads credentials once and resolves the account by existing Identity normalized username rules. It uses the existing SignInManager password/CanSignIn/lockout checks and bcrypt registration; it never compares a hash itself or accepts a claimed SubjectId. Preserve current ordinary login semantics, including lockoutOnFailure:false for the password stage, rather than silently changing account policy. If the account requires a second factor, password alone never issues a session: validate the supplied authenticator code using the configured Identity authenticator token provider, or consume a supplied recovery code through existing UserManager recovery-code support. Retain the corresponding existing failed-factor/lockout and successful-factor counter semantics; no remembered-browser bypass is available to headless execution. Missing required factor yields AdditionalAuthenticationRequired with no session; unsupported factor/account mechanisms deny rather than weakening requirements. No login cookie or bearer token is issued. Tests compare eligibility/lockout/factor behavior with the existing Identity managers, including failure of counter persistence.

Only the adapter's private session implementation, minted after successful authentication, is accepted by that adapter instance. A caller implementing IOctoberOperatorSession or substituting an instance from another authority is rejected. The session records authenticated subject, original security stamp, authority identity, expiry and closed state; it retains no password/factor. It is nonserializable and process-local. Dispose closes it, and expiry or discovered loss of authority latches it closed; a later restored account needs fresh authentication. Every protected operation rereads current account/stamp/eligibility/lockout/role rather than trusting claims, a tracked entity, old manager scope or startup success. Authentication grants no target or source permission by itself.

A real ManagementAccess Administrator or SystemOwner is required for setup. For a System member require current valid System and ownership assignment unless Administrator; membership must match. For standalone Repository require live ManagementAccess and SystemId null. Exact target must be configured/allowlisted and connection descriptor checked. A shared database login does not map to a user and SET ROLE does not supply operator identity. The startup session is never the principal for Web browser/Knowledge reads; those keep their current user/purpose/mode authorization under owning stories.

## Selection and unchanged Core setup policy

Prepare input carries target/batch, distinct Repository/System/root-version/label tuples and typed Global/System/Repository context references. No subject, physical path, connection or context value is caller authority. AuthorizeSelectionAsync authenticates/current-checks the session and exact target before materializing context. It returns a copied canonical OctoberSetupRequest with computed SelectionDigest and matching trusted OctoberAuthorizedSelection, or no payload on any failure. A supplied nonempty inconsistent digest denies. It checks authoritative management records in one bounded consistent read transaction. Recheck current subject immediately before issuing the result; no distributed transaction between optional separate Identity and management is claimed.

For select, PrepareRequest is null. After current subject and target authorization, the adapter reads only exact-batch setup metadata needed to reconstruct references/root/membership (not historical context exposure), validates current permissions and returns fresh trusted selection. Missing batch yields MissingBatch; a different batch already occupies this database yields BatchMismatch. No latest/other-target search occurs. Stored ManagementAuthorityId must equal the configured live authority before any historical capability/text is returned. Both modes use the same organization/context rules.

The existing concrete Core seam and records in `src/dotnet/Vulgata.Core/DomainServices/OctoberBatchSetupContracts.cs` remain unchanged:

- OctoberBatchSetupPolicy(IOctoberSetupWriter).PrepareAsync(request, authority, cancellationToken).
- IOctoberSetupWriter.CommitAsync(request, authority, cancellationToken).
- OctoberSetupRequest(TargetId, BatchId, Repositories, ContextSelections, SelectionDigest).
- OctoberRepositorySelection(RepositoryId, SystemId?, RootBindingIdentity, OperatorLabel).
- OctoberContextSelection(Scope, RecordId), Scope = Global/System/Repository.
- OctoberAuthorizedSelection(TargetId, BatchId, SubjectId, CanonicalDatabaseIdentity, RepositoryIds, SelectionDigest, SelectedContext).
- OctoberSelectedAdvisoryContext(ManagementAuthorityId, immutable Repositories, Entries, Differences).
- OctoberAdvisoryContextEntry(RepositoryId, Scope, Applicability, OriginRecordId?, Value?, EvidenceKind), applicability Selected/NotSelected/NotApplicable, kind AdvisoryNotSourceProof.
- OctoberContextDifference(RepositoryId, LeftScope, RightScope).
- OctoberPrepareResult(Status, CommittedBatch?), statuses Created/AlreadyIdentical/Conflict/Denied/AuthorityUnavailable/Cancelled/StorageFailure.
- OctoberCommittedBatch(TargetId, BatchId, CanonicalDatabaseIdentity, SelectionDigest, SelectedAtUtc?).

Core copies mutable request/reference collections before validation/forwarding. Missing/default trusted context denies even if no text is selected. Repositories must exactly match request and authority sets. Each Repository has exactly Global/System/Repository entries, including explicit NotApplicable System for standalone and NotSelected applicable scopes without chosen reference. Selected origins must be current, accessible and consumed by the request; values preserve legitimate null/empty. Only one Global reference is allowed; System/Repository references apply to exactly matching current membership. No flattening or promoted source fact.

Differences contain exactly each ordered Global < System < Repository pair of Selected scopes for one Repository whose values differ ordinally, with no duplicates/invented/cross-Repository pairs. Core recomputes and denies invalid entries/differences before writer access.

Canonical v2 digest remains lowercase SHA-256 of UTF-8 JSON array written by Utf8JsonWriter default encoder without BOM/whitespace:
`[2,targetId,batchId,repositories,contextReferences,managementAuthorityId,entries,differences]`.
GUIDs are lowercase invariant D strings. Repositories sort by ordinal Guid text and encode [repositoryId,systemId-or-null,rootBindingIdentity,operatorLabel]; references by scope then Guid encode [scope,recordId]; entries by Repository then scope encode [repositoryId,scope,applicability,originRecordId-or-null,value-or-null,evidenceKind]; differences by Repository/left/right encode [repositoryId,leftScope,rightScope]. Wire names remain global/system/repository, selected/not-selected/not-applicable, advisory-not-source-proof. Preserve string contents; reject duplicate identities, unknown enums and unpaired surrogates. Subject authentication, session expiry and SelectedAtUtc are not content-digest fields. OctoberSelectionDigest.Compute(request, context) ignores request.SelectionDigest and throws ArgumentException for structurally invalid inputs, mapped to Denied. There is no new digest version merely because trust transport changes.

The writer atomically commits all setup rows or none, stamping one trusted UTC SelectedAtUtc on first commit. Created/AlreadyIdentical require a matching committed identity and nonnull UTC time; failures have no identity/time/capability. Identical retry preserves original time; changed context/request conflicts. Cancellation before commit leaves no selectable partial batch; an acknowledged commit cannot be relabelled failure. Uncertain acknowledgement is resolved by exact lookup/replay.

The existing IOctoberBatchSelector.BindAsync(OctoberStartupSelection, CancellationToken) returns Selected/MissingTarget/MissingBatch/BatchMismatch/Denied/AuthorityUnavailable/Cancelled; only Selected carries bound capability. OctoberStartupSelection carries TargetId, BatchId and fresh trusted Authority. Require exact stored/current authority/subject/target/batch and stable organizational/root/reference identity. Current advisory text may differ from history under the same authority; selected historical values/digest/time remain unchanged. IOctoberHistoricalBoundSetupData.Snapshot is the existing OctoberBoundSetupSnapshot(Request, Authority, SelectedAtUtc). Different authority with colliding IDs denies before text/capability. No raw connection/EF context escapes the Core-facing result.

## Generator and Web startup

Retain IOctoberGeneratorModule.ExecuteAsync(command, CancellationToken) and closed OctoberGeneratorExecutionResult statuses Prepared/Selected/Denied/AuthorityUnavailable/TargetUnavailable/Cancelled. Revise OctoberGeneratorCommand to (OctoberGeneratorMode Mode, string TargetId, Guid BatchId, OctoberSetupRequest? SetupRequest): remove ActivationGrant. Prepare requires a request; select rejects one. The concrete constructor becomes OctoberGeneratorModule(IOctoberLocalAuthority authority, IOctoberConfiguredTarget target, string expectedManagementAuthorityId). It authenticates, obtains current selection, validates expected authority, forwards the returned canonical request/authority to the selected target, and settles/disposes its session at command completion. This slice exits after setup/selection. Its result contains only committed identity or bound setup metadata, never its session, a source context, a source callback or an approval issued for later use. Neither mode constructs the source verifier/issuer/binding or invokes source functions. Later generation must define its own authenticated operation lifetime in its owning story; a disposed setup session is never extended, captured for future use or persisted.

Keep IOctoberConfiguredTarget.ExecuteAsync(command, authority, token) and the existing OctoberConfiguredTarget target-identity/migration/policy/selector seam. Replace unconfigured placeholders with actual Npgsql/EF adapters at implementation. Prepare checks configured and connected identity before October migration, never calls BindAsync before first commit, and never migrates Identity/domain. Select invokes shared selector only for an existing exact batch. All exits are sanitized, nonzero on failure, no provider/source work implied by Prepared or Selected.

Web ConfigureOctoberHost uses the existing explicit DefaultServiceProviderFactory and deferred ConfigureContainer callback to resolve final configuration once. Preserve environment-dependent ValidateScopes/ValidateOnBuild, early ordinary service registration once, controlled test overrides, no temporary provider. In selected-data only, replace the connection-specific Identity/domain context options/factories with final immutable selected configuration consistently, including all factories; register headless authority and exact October target. Default ordinary registrations remain untouched. Never leave ordinary services reading an earlier database while October services use the new one. Test both actual DbContext and factory options. The callback does no network/database work.

After Build, selected-data Web authenticates its explicitly configured startup operator, current-authorizes exact selection and calls shared BindAsync before serving; missing/invalid selection fails visibly. It performs no October migrations or authority bootstrap/seeding. Existing ordinary migration/seeding executes only in ordinary mode; selected-data validates required existing Identity/domain schema compatibility without applying an unapproved migration. Dispose its startup session after binding; the selected setup is metadata, not continuing source/disclosure authority. Later requests authenticate their own browser user and recheck current authorization. No source context is issued at Web startup. The four former October activation/approval routes are not registered in either mode. Runtime config reload never changes connection/mode/batch/routes/provider binding.

## Current source authority without Web

Reuse October approval rows and exact binding. The only new caller-facing approval operations are trusted in-process calls, not model tools or HTTP endpoints. IssueSourceApprovalAsync takes an adapter-issued current session, exact bound snapshot and Repository ID. It rereads selected batch, authority, subject, membership, target and root version, and uses the stored label. It creates a random opaque ApprovalIdentity with ten-minute maximum UTC expiry and IsRevoked=false in the selected October DB. Expiry is no later than session expiry. It returns Issued with unchanged PreparedSourceApproval and expiry iff the row committed; failures return neither. No arbitrary subject/purpose/root input, activation grant, source bytes or physical path is accepted.

RevokeSourceApprovalAsync requires current authenticated subject authority for the bound target and approval. Only its subject or a current Administrator may revoke; exact target/batch authority is still mandatory. Unknown/foreign records deny without existence leakage. Idempotent repeat returns AlreadyRevoked. This is a trusted application operation for current authority, not a new management UI. Tests and future approved host integration invoke the same production operation. Identity role/removal/stamp/lockout changes and approval expiry independently revoke effective permission even without a revoke call.

VerifySourceApprovalAsync rereads current session identity/security stamp/eligibility, roles/assignments, Repository/System, target/batch/ManagementAuthorityId, immutable root binding and exact approval record on every call. No tracked or positive cached result. It validates all seven approval fields and WorkerSourceRead, expiry/revoked state and authenticated subject. Definite mismatch returns Revoked with no CurrentApproval. Failure to establish current authority returns Unavailable with no CurrentApproval. Cancellation propagates OperationCanceledException when the supplied token is cancelled so the existing issuer preserves Cancelled/DeadlineExceeded.

Revise the existing OctoberWebApprovalCheck delegate name to OctoberCurrentApprovalCheck while retaining its (targetId, PreparedSourceApproval, CancellationToken) -> Task<PreparedSourceAuthorityResult> shape; it is no longer HTTP-specific. OctoberPreparedSourceAuthorityVerifier(targetId, currentApprovalCheck, hasCurrentRootBinding) retains exact tuple/root matching. A trusted integration caller captures its own active session and delegates to IOctoberLocalAuthority.VerifySourceApprovalAsync. The immutable local mapping resolves the physical path, while the fresh database binding/approval allows live revocation or version loss despite immutable process configuration. No runtime path retargeting is supported.

PreparedSourceBinding and the unchanged PreparedSourceContextIssuer receive exact tuple/path/limits. Only Issued supplies one IPreparedSourceReadContext. The caller lends it to PreparedSourceFunctions.Create(), which supplies exactly list_prepared_source/read_prepared_source/search_prepared_source, and disposes after all borrowed calls settle. One Repository per context; no session/handle/continuation persistence or reactivation. Identity/database outage, revocation and cancellation are checked at 18.1's existing pre-read, between-chunk and pre-disclosure checkpoints. Repeated database checks have a performance cost, accepted here for correctness; no cached allow is introduced.

### Callable adapter integration and its current caller

Story 18.4 delivers the production authority, verifier and root-binding adapters, not a generation command. Its controlled integration harness is the current caller that constructs them together. The existing `OctoberPreparedSourceBindingTests.CreateIntegratedBindingAsync` construction is evidence of this harness, not evidence that generator Program or ExecuteAsync owns a source session. Required integration continues to invoke the real direct authority and issuer; a fake always-current approval or copied source algorithm is not an alternative.

Keep these existing public shapes without adding a composition factory:

- `PreparedSourceRootBinding(string TargetId, Guid BatchId, Guid RepositoryId, string RootBindingIdentity, string PhysicalRootPath)` is trusted configuration only.
- `OctoberPreparedSourceAuthorityVerifier(string targetId, OctoberCurrentApprovalCheck currentApprovalCheck, Func<PreparedSourceApproval, bool> hasCurrentRootBinding)` implements the unchanged current-authority interface.
- `PreparedSourceContextIssuer(IPreparedSourceCurrentAuthorityVerifier authority)` remains the 18.1 concrete issuer.
- `PreparedSourceBinding(PreparedSourceContextIssuer issuer, string targetId, IReadOnlyCollection<PreparedSourceRootBinding> trustedRoots)` copies/validates the immutable exact mapping; `HasCurrentRootBinding(PreparedSourceApproval) -> bool` and `IssueAsync(PreparedSourceApproval, PreparedSourceGrantLimits, CancellationToken = default) -> Task<PreparedSourceIssueResult>` retain their existing results and cancellation semantics.
- `PreparedSourceFunctions(IPreparedSourceReadContext context).Create() -> IReadOnlyList<AIFunction>` borrows the issued context; its three closed schemas remain 18.1-owned.

The integration caller authenticates through the same OctoberLocalAuthority instance and owns that session. It obtains exact committed bound setup through the actual policy/store/selector, then calls IssueSourceApprovalAsync with that session and Repository. It constructs the verifier with a callback to that same authority/session and a root predicate delegating to the completed binding's HasCurrentRootBinding; the captured binding is assigned before any IssueAsync call. The issuer uses this verifier, and the binding uses that issuer and the trusted map. Neither adapter owns or disposes the session. No callback, issuer or context escapes the caller's session lifetime. This describes the existing callable composition, not a second implementation or a new public factory.

Only successful issuance transfers a context to the integration caller. The caller keeps the session alive, lends the context to the existing functions, and awaits all calls. On normal completion, cancellation or failure it settles outstanding calls, asynchronously disposes every owned context, and only then disposes the session and backing fixture/service provider. On failed issuance there is no context to dispose, but the session still closes. Deliberate early session disposal is a negative test: current verification denies subsequent disclosure, closes the context and exposes no continuation; it is never a valid production continuation strategy. Separate Repository contexts never share identity or cursors.

This adapter integration satisfies the setup story's source association/current-authority adaptation. It does not qualify executable source generation, which remains absent here. Actual prepare/select and selected-data startup tests separately prove their short-lived authentication-session disposal and metadata-only result. Adding a dummy source read, unused factory, Worker callback or new command to create a production call site would expand this boundary and is not part of this reconciliation. Existing adapter behavior uses truthful passing integration/regression; only demonstrated changed production behavior requires new behavioral RED/GREEN.

## Verification and failure contract

All public async calls accept cancellation, use bounded fresh scopes and close resources. Identity APIs without a CancellationToken are awaited with bounded database commands and checked before/after; do not abandon pending work on disposed services. Incoming source deadline is never extended. Authentication/selection/approval failure exposes no sensitive payload. Logs omit credentials/factors, source/context text, physical paths, approval IDs and raw exceptions.

Required proof: actual batch process prepares/selects with Web stopped; startup-configured Web switches A to B only after restart; all domain/October factory options agree; separate Identity is explicit and does not redirect management; ordinary startup unchanged. Authentication uses actual existing Identity managers with controlled input; tests cover factor/lockout/stamp/current-role semantics. Production direct authority plus real 18.1 issuer proves current revocation/outage and tuple/root isolation. Existing v2 digest/context/selector/storage assertions remain regression. Changed host/auth behavior needs RED/GREEN; old HTTP tests retain historical meaning and are reconciled rather than forcing obsolete routes to survive.

Use current testing.md and owned runner resources for real PostgreSQL/process checks. No TLS or paid provider call is required by this corrected transport. Story 18.1 [final verification](../../016-contained-source-tools/evidence/final-verification-01.json) remains its own dependency evidence, not acceptance of this adapter.
