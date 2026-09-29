# Shared October Setup and Selection Contract

This fixes Story 18.4's generator/Web setup, selection and authorization surface. The Web routes below are required API contracts; this document does not redefine Story 18.1's Worker-facing source tools. Both hosts use the same Core policy and Infrastructure setup writer/selector rather than separate persistence rules.

## Programmatic setup request

Caller-controlled inputs: an allowlisted target ID, exact nonempty batch ID, chosen System/Repository identities (System optional for a standalone Repository), selected applicable Global/System/Repository context references, and an opaque prepared-root binding version plus descriptive operator label for each selected Repository. The trusted generator configuration, not the request, maps each exact `(target identity, batch, Repository, binding version)` to a physical prepared directory. A caller-supplied operator subject, connection string, unlisted target or physical source path is never authority. One immutable binding row per `(BatchId, RepositoryId)` is accepted only after exact setup checks; no setup request grants a root, Git revision or Worker tool by itself.

The explicitly enabled authorization-only October Web bootstrap obtains the operator from its existing authenticated Identity principal and checks live `ManagementAccess` (Administrator or SystemOwner backed by `UserManager`). It validates the target ID against deployment-operator-owned configuration, not database reachability. It checks a System member through current System-owner visibility and Repository membership. For a standalone Repository, it requires current `ManagementAccess`, an existing Repository with `SystemId == null`, and no fabricated System/owner. It then issues a short-lived, purpose-bound Data Protection grant containing the server-derived operator subject and exact target/batch/Repository selection; the raw request cannot set the subject in that grant. This is a new setup authorization seam using existing Identity, not a new login or the Story 18.1 source grant.

Authorization-only October Web validates the grant without opening an October target; the headless generator presents it to that Web authorization endpoint. The endpoint validates protection, expiry and scope and rechecks current Identity role, Repository visibility and deployment target allowlist. It returns a sanitized decision and canonical target identity, never credentials. Generator `prepare` resolves the target ID only through its trusted local allowlist, confirms its canonical descriptor agrees with Web and checks connected-database identity read-only before any October migration. The generator does not reuse Web's request-scoped `IUserContext`, accept a CLI user ID as proof, or treat a successful database connection as authorization. Missing/forged/expired/substituted grants or an unreachable authority fail closed.

The shared policy consumes that trusted authorization result, verifies exact target/batch/Repository agreement, root-binding version and context origins, preserves value/scope/origin/conflict, and commits all setup-owned records atomically within the October database. Ordinary authority and batch data have no cross-database foreign key or distributed transaction, so protected use repeats grant/current Identity, Repository and target checks. It returns the exact target/batch identity and a sanitized outcome. Failed validation, cancellation or persistence leaves no selectable partial batch. An uncertain acknowledgement is resolved by exact target/batch lookup and immutable-input comparison, never by a new target or blind duplicate write.

## Callable Web authorization API

All routes are JSON-only, under plural `/api/` resources, use closed request DTOs with validation, accept a `CancellationToken`, and return sanitized JSON or `application/problem+json` ProblemDetails. Explicit authorization-only October Web bootstrap starts with ordinary Identity/Data Protection and deployment target/certificate allowlists and exposes only the first two grant/validation routes below. It registers no October context, selector, migration, selected-data service or source-approval route and never opens an October target. Selected-data October Web starts only after an exact batch exists and may expose all four routes. Default ordinary Web has none of these October routes or services. No caller may submit a subject ID, connection string, physical root, source tool name or provider secret.

| Method and route | Caller proof and closed request | Success result |
| --- | --- | --- |
| `POST /api/october-batch-activation-grants` | Existing authenticated Web Identity cookie, live `ManagementAccess`, and validated antiforgery token. Body: `targetId` (opaque string), `batchId` (nonempty Guid), `repositories` (nonempty distinct entries of `repositoryId`, nullable `systemId`, `rootBindingIdentity`, `operatorLabel`), and `contextSelections` (distinct entries of `scope: global|system|repository` plus `recordId: Guid`; the record ID is respectively `GlobalContext.Id`, `System.Id` or `Repository.Id`). No caller context text is accepted. | `201` with protected `activationGrant`, `expiresAtUtc`, exact `targetId`/`batchId` and canonical `selectionDigest`; server derives `SubjectId` and grant purpose. The grant contains the exact selected Repository/root/context digest and cannot be retargeted. |
| `POST /api/october-batch-activation-grants/validations` | Allowlisted generator mutual-TLS client certificate plus body `activationGrant` and the complete same `targetId`, `batchId`, `repositories`, `contextSelections` selection DTO. Certificate identity is separately authorized for that target; Web recomputes the canonical digest and compares the protected grant rather than trusting a submitted digest. | `200` with sanitized `subjectId`, `targetId`, `batchId`, canonical target identity, approved Repository IDs, `selectionDigest` and grant expiry; no connection descriptor or credential. |
| `POST /api/october-prepared-source-approvals` | Same generator certificate plus current `activationGrant`, exact `targetId`, `batchId`, `repositoryId`, `rootBindingIdentity`. Valid only after selected setup exists and its immutable binding matches. | `201` with opaque `approvalIdentity`, exact approved batch/Repository/subject/purpose/root-binding/label tuple and `expiresAtUtc`; Web stores the revocable record in the selected October database, not the raw token. |
| `POST /api/october-prepared-source-approvals/verifications` | Same generator certificate plus `targetId` and all seven fields of the existing `PreparedSourceApproval` (`BatchId`, `RepositoryId`, `SubjectId`, `Purpose`, `ApprovalIdentity`, `RootBindingIdentity`, `OperatorLabel`). The target must equal the Web host's selected target. | `200` with only `status: current` when the exact record, expiry, live Identity/Repository/target authority and root-binding version all match. The generator-side `IPreparedSourceCurrentAuthorityVerifier` returns `Current` with the requested approval only after this exact success and its own local mapping check. |

The generator proves its own identity with a deployment-owned client certificate validated by the Web TLS boundary against an explicit allowlist and target scope. The certificate is not a user identity; the operator grant is not generator authentication. Missing, expired or untrusted certificate gets `401`, and a valid generator certificate outside the selected target scope gets `403`. The internal API accepts no cookie-only or caller-supplied certificate header substitute. An ingress may terminate TLS only when its authenticated certificate identity is conveyed over a separately protected trusted hop; otherwise Web must validate the client certificate itself. Production certificate provisioning, rotation and access require explicit owned resources.

The selection digest is SHA-256 over one canonical encoding of the complete validated selection DTO with Repository and context entries sorted by their typed IDs; Web computes it at issuance and validation, and the shared setup policy compares the trusted returned digest with its own exact input before writing. Sorting does not erase duplicate entries; duplicates are rejected first. A stale, changed or substituted context reference fails live validation. The digest binds request identity, not source contents or a Git revision.

Anonymous operator requests get `401`; authenticated users lacking current `ManagementAccess` or permitted Repository visibility get `403`. Invalid closed DTOs or missing antiforgery proof get `400` ValidationProblem/ProblemDetails. Forged, expired, revoked or substituted activation grants and mismatched prepared-source approval tuples get `403` with only a stable sanitized reason code; no token, path, subject details or record existence leaks. An unavailable Identity/October authority or failed internal Web dependency gets `503` ProblemDetails with `authority_unavailable`, never a cached allow. No `/api/` route redirects to login or re-executes an HTML error page. The generator maps exact current verification to Story 18.1 `Current`, a definite `403` to `Revoked`, and `503`, transport failure or indeterminate response to `Unavailable`; issuance then follows Story 18.1's existing closed statuses.

Each handler and generator client forwards the request cancellation token through live authority and selected-database checks. The generator sets a bounded HTTP deadline no longer than the remaining source-grant deadline; cancellation/timeout stops the current attempt and no cached approval is reused. Operator startup grants and source approvals have explicit short expiries; a fresh activation grant is needed on every generator restart. Approval verification is repeated at issuance and Story 18.1's disclosure checkpoints, including after role, membership, target or root-binding changes. A later restored approval requires fresh issuance, never resurrection of a closed context.

## Process startup selection

The in-scope `src/dotnet/Vulgata.OctoberGenerator` console host has explicit `prepare` and `select` modes and no analysis loop. It takes mode, opaque target ID, exact batch ID, protected activation grant and, for `prepare`, the validated setup selection from a declared host configuration source. It never accepts a caller connection string or physical source path. First-batch `prepare` authenticates with the allowlisted client certificate against authorization-only bootstrap Web, validates the operator grant and local target mapping, checks the connected empty target identity read-only, then and only then runs authorized October migrations and atomically calls the shared `PrepareAsync` setup policy. It does not call `BindAsync` before the first batch exists. A failed check leaves the empty target unmigrated and no selectable batch. `select` validates the grant and calls the shared `BindAsync` selector for an existing exact batch, binds the selected setup capability for its process and emits sanitized target/batch/status before exiting; it runs no migration. Missing target, batch or mismatch exits nonzero before downstream October data access. Later stories may extend this host for generation without replacing its selection contract.

The shared callable selector is `IOctoberBatchSelector.BindAsync(OctoberStartupSelection selection, CancellationToken cancellationToken)` where `OctoberStartupSelection` carries validated target ID, exact nonempty batch ID, and the trusted Web authorization result. It returns `OctoberSelectionResult` with a closed `Selected`, `MissingTarget`, `MissingBatch`, `BatchMismatch`, `Denied`, `AuthorityUnavailable`, or `Cancelled` status and a bound October setup-data capability only for `Selected`; no raw connection descriptor/EF context is exposed through the Core-facing result. A missing or mismatched pair cannot resolve a previous/latest batch.

### One callable Core setup policy

The only Core setup policy callers invoke is `Vulgata.Core.DomainServices.OctoberBatchSetupPolicy`. Its public shape, including constructor, request/authority record construction and result, is fixed for test authors and both host compositions:

```csharp
public sealed class OctoberBatchSetupPolicy
{
    public OctoberBatchSetupPolicy(IOctoberSetupWriter writer);
    public Task<OctoberPrepareResult> PrepareAsync(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default);
}

public interface IOctoberSetupWriter
{
    Task<OctoberPrepareResult> CommitAsync(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default);
}

public sealed record OctoberSetupRequest(
    string TargetId,
    Guid BatchId,
    IReadOnlyList<OctoberRepositorySelection> Repositories,
    IReadOnlyList<OctoberContextSelection> ContextSelections,
    string SelectionDigest);
public sealed record OctoberRepositorySelection(
    Guid RepositoryId, Guid? SystemId,
    string RootBindingIdentity, string OperatorLabel);
public enum OctoberContextScope { Global, System, Repository }
public sealed record OctoberContextSelection(
    OctoberContextScope Scope, Guid RecordId);
public sealed record OctoberAuthorizedSelection(
    string TargetId, Guid BatchId, string SubjectId,
    string CanonicalDatabaseIdentity,
    IReadOnlyList<Guid> RepositoryIds,
    string SelectionDigest);
public enum OctoberPrepareStatus
{
    Created, AlreadyIdentical, Conflict, Denied,
    AuthorityUnavailable, Cancelled, StorageFailure
}
public sealed record OctoberCommittedBatch(
    string TargetId, Guid BatchId,
    string CanonicalDatabaseIdentity, string SelectionDigest);
public sealed record OctoberPrepareResult(
    OctoberPrepareStatus Status,
    OctoberCommittedBatch? CommittedBatch);
```

`IOctoberSetupWriter` is the existing planned Infrastructure persistence port injected into the policy constructor, with one `CommitAsync(OctoberSetupRequest, OctoberAuthorizedSelection, CancellationToken) -> Task<OctoberPrepareResult>` operation; it is not a second setup policy or a caller-facing database selector. A null writer is programmer misuse (`ArgumentNullException`); malformed/foreign field values are sanitized `Denied`. Trusted application composition constructs `OctoberAuthorizedSelection` only from the live Web validation result and deployment target mapping; it is never bound from an HTTP/CLI request body. The policy checks exact nonempty target/batch, Repository set and canonical selection digest equality between request and authority before the writer can open the owned database. The writer owns the one transaction and exact immutable-input comparison on uncertain acknowledgement.

`Created` and `AlreadyIdentical` are the only success statuses: `CommittedBatch` is nonnull, equals the request's exact target/batch/digest and the authority's canonical database identity, and denotes a committed `Selectable` batch with all setup-owned rows in the selected database. `AlreadyIdentical` means an exact immutable retry observed that committed row without overwrite. `Conflict`, `Denied`, `AuthorityUnavailable`, `Cancelled` and `StorageFailure` have `CommittedBatch == null` and confer no bound capability or proof of partial success. Cancellation before commit leaves no selectable batch; cancellation after an acknowledged commit cannot relabel that commit as failure. No raw connection, physical path, token or exception is returned. Generator `prepare` may invoke this policy after live bootstrap validation and authorized empty-target migration without an existing batch; it never calls `BindAsync` first. After a success, a later generator `select` or selected-data Web must independently call `IOctoberBatchSelector.BindAsync` to obtain an existing-batch bound capability. `PrepareAsync` does not issue one.

After the first batch commits, selected-data October Web and generator `select` each require a protected activation grant, exact allowlisted target ID and batch ID. Each validates authorization and its local canonical target mapping before its selected setup lookup. The shared selector binds one fixed connection/factory to that database, verifies the batch exists there and that its setup identity matches, and makes only that selection available to October consumers. Neither selected-data mode migrates an empty target. No latest/previous-batch query, other-database probe, dynamic EF schema, per-request selection or legacy-wiki fallback is permitted. Switching either element requires process restart and fresh authorization.

October Web keeps ordinary Identity/domain database configuration separate from the selected October database. Default ordinary Web startup has no October context registration, model configuration, routes or migration path. Explicit authorization-only bootstrap adds only the two closed grant/validation routes over existing Identity/Data Protection and allowlists; it does not select, migrate or read an October database and exposes no downstream October service. Only generator `prepare`, after live authorization and connected-target identity check, may migrate an explicitly owned empty October target. Protected setup and later selected-data October use repeat live authorization, including after an operator role, Repository membership or target allowlist change. The startup grant is renewed on every generator restart; an unchanged root-binding version needs no separate reapproval merely because the process restarted.

## Prepared-source approval and Worker composition

After setup, October-enabled Web holds a short-lived, revocable current source-approval record in the selected October database keyed by opaque `ApprovalIdentity`. It contains the exact batch, Repository, real Identity subject, `WorkerSourceRead` purpose, root-binding version, descriptive label, expiry and current/revoked status, but no raw activation token or physical path. The configured generator resolves the immutable version to its deployment-owned physical directory and verifies that its mapping agrees with the selected row before issuance. A missing, rotated or revoked mapping denies new issuance and closes an existing context on its next mandatory current-authority check; no old version falls back to another root.

Trusted Worker-session composition forms the existing `PreparedSourceApproval(BatchId, RepositoryId, SubjectId, Purpose, ApprovalIdentity, RootBindingIdentity, OperatorLabel)` and `PreparedSourceGrantRequest(Approval, PhysicalRootPath, Limits)`. It constructs `PreparedSourceContextIssuer` with a production `IPreparedSourceCurrentAuthorityVerifier` that rechecks the exact current record and live Web Identity, Repository, target and root-binding authority at issuance and throughout the context. Exact match returns `Current` with the same approval; revoked/changed/expired authority returns `Revoked`; unreachable or indeterminate authority returns `Unavailable`. The activation grant alone is not a cached allow. No model-visible argument chooses any authority field, root or credential.

Only `PreparedSourceIssueStatus.Issued` provides one `IPreparedSourceReadContext`. The trusted Worker-session caller owns its async disposal after all calls settle and lends it to Story 18.1's `PreparedSourceFunctions.Create()`, which exposes exactly `list_prepared_source`, `read_prepared_source` and `search_prepared_source`. Each context is for one Repository and approval; a batch with several Repositories issues separate contexts. Source operations, budgets, relative paths, sanitized outcomes, cancellation, revocation closure and one-use continuation behavior remain Story 18.1's [accepted contract](../../016-contained-source-tools/contracts/prepared-source-tools.md). No context, handle, physical path or continuation is durable. A different physical root requires new explicit authorization and a new batch or separately reviewed rebind design, not an update to the existing immutable setup row.

## Outcomes and failure classes

Report only sanitized target/batch identifiers and one of: missing selection, forged/expired/substituted grant, authorization service unavailable, unauthorized/unlisted target/operator, invalid organizational/context/root-binding selection, batch absent/mismatch, incomplete provider binding, protected-secret compatibility, migration/storage failure, cancelled, or selected. Prepared-source issuance separately preserves Story 18.1's closed issue statuses, including denied, authority unavailable, root unavailable, unsupported platform, cancelled and deadline exceeded; failure has no context. Logs must omit grants, connection credentials, provider keys, raw source, hidden reasoning and physical source paths. `selected` does not mean contained file tools, native CodeGraph, paid provider inference or Knowledge publication are ready.

## Story 18.1 integration boundary

Story 18.1 is completed and integrated at `3632b2cc`; its production types and [final verification](../../016-contained-source-tools/evidence/final-verification-01.json) fix the callable source seam. This contract supplies Story 18.4's production approval and setup association only. The corrected definition still needs deterministic validation and independent review before task generation; integration alone does not qualify this adapter or grant implementation authority.
