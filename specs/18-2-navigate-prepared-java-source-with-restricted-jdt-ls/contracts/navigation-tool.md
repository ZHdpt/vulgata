# Story 18.2 Navigation and Trusted Workspace Contracts

This is a plan contract for the Story 18.2-owned shared physical-workspace seam and its JDT-LS consumer. It preserves the integrated Story 18.1 read contract at `specs/016-contained-source-tools/contracts/prepared-source-tools.md`. The new, narrow in-place `.codegraph` state grant is a proposed addition; Story 18.3 consumes it only after fresh review/approval and a separately authorized, identifiable verified checkpoint. This document is neither approval nor an implemented API.

## Shared trusted physical-workspace seam

The trusted application supplies the existing exact `PreparedSourceApproval` tuple (batch, Repository, subject, `WorkerSourceRead`, approval identity, root-binding identity and operator label), physical prepared root, selected tool purpose and lease deadline. `WorkerSourceRead` authorizes the read context only; a separate current tool-purpose authorization is mandatory for `JdtNavigation` or `CodeGraphPreparation`. A missing verifier/authorizer has no default allow implementation. The root is retained as a no-follow handle with stable object identity (volume/file index); its mutable directory last-write time is not the lease's replacement check. This does not alter Story 18.1's independent reader identity/change policy or prove source bytes unchanged. No root handle/path is exposed through `IPreparedSourceReadContext` or Agent arguments.

The smallest intended callable surface is in `Vulgata.Infrastructure.Source`; final C# names may change only while preserving these construction, return, lifetime and authority semantics:

```csharp
public sealed class PreparedToolWorkspaceIssuer
{
    public PreparedToolWorkspaceIssuer(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization);
    public Task<PreparedToolWorkspaceIssueResult> IssueAsync(
        PreparedToolWorkspaceGrantRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PreparedToolWorkspaceGrantRequest(
    PreparedSourceApproval Approval,
    string PhysicalRootPath,
    PreparedToolPurpose ToolPurpose,
    TimeSpan Deadline);
public enum PreparedToolPurpose { JdtNavigation, CodeGraphPreparation }
public enum PreparedToolUseAuthorityStatus { Current, Denied, Unavailable }
public sealed record PreparedToolUseAuthorityResult(
    PreparedToolUseAuthorityStatus Status);
public interface IPreparedToolUseAuthorizer
{
    Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
        PreparedSourceApproval approval,
        PreparedToolPurpose toolPurpose,
        CancellationToken cancellationToken = default);
}
public sealed record PreparedToolWorkspaceIssueResult(
    PreparedToolWorkspaceIssueStatus Status,
    PreparedToolWorkspaceLease? Lease);
public enum PreparedToolWorkspaceIssueStatus
{
    Issued, Denied, AuthorityUnavailable, RootUnavailable,
    UnsupportedPlatform, Cancelled, DeadlineExceeded
}
public enum PreparedToolWorkspaceLifetime
{
    Open, AuthorityClosed, ExpiredOrCancelled, Disposed
}
public enum PreparedToolWorkspaceCheckStatus
{
    Current, Denied, AuthorityUnavailable, RootUnavailable,
    Cancelled, DeadlineExceeded, Disposed
}
public sealed class PreparedToolWorkspaceLease : IAsyncDisposable
{
    public Guid RepositoryId { get; }
    public PreparedToolPurpose ToolPurpose { get; }
    public PreparedToolWorkspaceLifetime Lifetime { get; }
    public Task<PreparedToolWorkspaceCheckStatus> CheckCurrentAsync(
        CancellationToken cancellationToken = default);
    internal Task<PreparedToolWorkspaceCheckStatus> UseContainedRootAsync(
        PreparedToolPurpose expectedPurpose,
        Func<PreparedToolRootScope, CancellationToken, ValueTask> use,
        CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
    // Root handle and identity remain private; no public/model root/path property.
}

internal sealed class PreparedToolRootScope
{
    internal string GetPhysicalRootPathForTrustedToolBinding();
    internal bool IsSeparateDisposableStateRoot(string absoluteStateRoot);
    internal bool TryMapContainedFileUri(
        string rawProtocolUri, out PreparedSourceLocation? relativeLocation);
}
```

The status families distinguish issued/current, denied, authority unavailable, root unavailable, unsupported platform, cancelled and deadline exceeded without exposing trusted input. `Issued` has exactly one lease; every other status has none. Null or malformed construction/request inputs fail closed. The trusted caller owns async disposal after adapter calls settle. A `JdtNavigation` lease may cover one bounded JDT-LS session; Story 18.3 obtains a distinct `CodeGraphPreparation` lease for its own operation and cannot reuse JDT-LS permission or session lifetime. Cancellation/deadline close the affected lease; a new approval or restored verifier never reopens it.

Issuer and lease check the exact existing `PreparedSourceApproval` through `IPreparedSourceCurrentAuthorityVerifier` and the selected tool purpose through `IPreparedToolUseAuthorizer` at issuance, before each source/protocol operation, between bounded work chunks and immediately before disclosure. Revoked/changed approval, denied purpose or unavailable verifier/authorizer latches the lease closed, cancels owned work and discards buffered results. No cached allow is sufficient. The same grant/root identity is never silently rebound. Story 18.3 may use the lease for its native CodeGraph preparation without referencing JDT-LS classes. The lease constrains application-controlled operations and model-visible Worker tools, not a pinned native child with inherited host filesystem rights; Story 18.3 owns actual-footprint observation and readiness. JDT's restricted process boundary remains separate.

`UseContainedRootAsync` is the existing trusted physical-root read/binding operation; it is `internal` in Infrastructure and is never part of `IPreparedSourceReadContext`, a Worker function, a serialized result or a public path property. It rejects an unexpected tool purpose, non-open lease, unavailable/mismatched exact current approval, deadline/cancellation or changed canonical root object identity before invoking the callback. It serializes the short callback with lease disposal under one gate, reusing an internal check routine without recursively entering public `CheckCurrentAsync`, and rechecks current authority and pinned root object identity afterward. The callback receives a token linked to caller cancellation and lease deadline and only a short-lived `PreparedToolRootScope`, invalidated on completion/closure; the callback returns no physical path or arbitrary value. On failure or an exception, the operation returns a sanitized non-current status, and consumers discard derived bindings/results. This callback is limited to bounded root binding, separate JDT state-root comparison or URI mapping; no unbounded JDT protocol/navigation wait or Worker disclosure occurs inside it. A launch binding copied by trusted Infrastructure from the callback requires another live identity/authority check immediately before launch. The same stable object check applies to JDT and CodeGraph leases so legitimate `.codegraph` parent metadata changes do not close a JDT session; per-file evidence and a restricted child process, not root-directory mtime, establish source no-write claims.

`GetPhysicalRootPathForTrustedToolBinding` is callable only inside that internal callback by trusted adapters. It lets the JDT adapter create `rootUri` and the CodeGraph adapter bind its source input, while the lease retains the private root handle/identity and no path reaches Agent code. `IsSeparateDisposableStateRoot` resolves the configured JDT tool-state directory and rejects overlap in either direction or reparse redirection into the prepared root; it remains the JDT rule and is not weakened for CodeGraph. `TryMapContainedFileUri` takes the JSON-decoded protocol string before `Uri.TryCreate` or other URI/path normalization. First inspect exact raw syntax: require the canonical absolute local `file:` form for this host; reject backslashes, device/extended-length prefixes, UNC/authority hosts, query/fragment, empty/dot segments, malformed or encoded path syntax that can change interpretation, and non-file schemes. Only then derive a path and verify it through held-root handle-relative no-follow traversal. Reject outside roots, symlink/reparse escapes, nonexistent/unreadable targets and changed root identity. It yields only `PreparedSourceLocation` on success, never an outside physical path or raw URI. A `System.Uri` parameter cannot faithfully test raw Windows `\\?\\` or `\\.\\` device spellings because construction may reject or normalize them first. The production protocol parser must retain `JsonElement.GetString()` as the raw location string through this check; a test supplies literal raw protocol strings at that same seam. For every operation, current authority is checked again immediately before any Worker response; authority loss discards already mapped locations.

### Positive owned CodeGraph state binding

The existing root callback is a read/binding operation and `IsSeparateDisposableStateRoot` continues to reject in-root JDT state. A separate internal CodeGraph-only operation is proposed in `Vulgata.Infrastructure.Source`; it accepts neither an arbitrary state-entry name nor a caller-supplied directory. Trusted application composition must establish a non-model `PreparedCodeGraphWorkspaceOwnership` for an explicitly dedicated managed prepared workspace; no such workspace-manager service is implemented merely by this contract. The positive record binds the exact `PreparedSourceApproval` (including batch, Repository, subject, approval and root-binding identities), canonical physical root and stable observed root object identity (volume/file index, not mutable directory last-write time), workspace owner identity, fixed `.codegraph` name and, after exclusive creation, the created entry's physical identity. It is retained outside `.codegraph` across individual tool leases until disposition and must be recoverable and independently verified after restart. An absent/foreign/unverifiable record or a preexisting `.codegraph` entry cannot be adopted. A validated internal view of this positive binding is the only way Story 18.3's ordinary reader may classify this entry as application-owned tool state; the name alone is insufficient.

The intended internal callable shape is:

```csharp
internal sealed record PreparedToolDirectoryObjectIdentity(
    uint VolumeSerialNumber, uint FileIndexHigh, uint FileIndexLow);
internal enum PreparedCodeGraphOwnershipPhase
{
    Absent, Creating, Ready, Disposing, Removed, Quarantined
}
internal sealed record PreparedCodeGraphWorkspaceOwnership(
    Guid OwnershipId,
    long Version,
    PreparedCodeGraphOwnershipPhase Phase,
    PreparedSourceApproval Approval,
    string PhysicalRootPath,
    PreparedToolDirectoryObjectIdentity RootObjectIdentity,
    string WorkspaceOwnerIdentity,
    PreparedToolDirectoryObjectIdentity? StateEntryObjectIdentity);
internal enum PreparedCodeGraphOwnershipTransitionStatus
{
    Committed, Conflict, Unavailable
}
internal sealed record PreparedCodeGraphOwnershipTransitionResult(
    PreparedCodeGraphOwnershipTransitionStatus Status,
    PreparedCodeGraphWorkspaceOwnership? Record);
internal enum PreparedCodeGraphOwnershipLoadStatus
{
    Found, VerifiedAbsent, Unavailable
}
internal sealed record PreparedCodeGraphOwnershipLoadResult(
    PreparedCodeGraphOwnershipLoadStatus Status,
    PreparedCodeGraphWorkspaceOwnership? Record);
internal enum PreparedCodeGraphOwnershipViewStatus
{
    VerifiedOwned, VerifiedUnowned, Unavailable
}
internal sealed record PreparedCodeGraphOwnershipViewResult(
    PreparedCodeGraphOwnershipViewStatus Status,
    PreparedCodeGraphWorkspaceOwnership? Record);
internal interface IPreparedCodeGraphOwnershipStore
{
    // Every transition is durable and atomic, compares OwnershipId and Version,
    // and returns its newly committed record only after persistence succeeds.
    Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        CancellationToken cancellationToken = default);
    // Narrow lookup on the same fixed ledger so restart can recover the prior ID.
    Task<PreparedCodeGraphOwnershipLoadResult> FindExactWorkspaceAsync(
        PreparedSourceApproval approval,
        string physicalRootPath,
        PreparedToolDirectoryObjectIdentity rootObjectIdentity,
        string derivedWorkspaceOwnerIdentity,
        CancellationToken cancellationToken = default);
    Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
        PreparedCodeGraphWorkspaceOwnership absent,
        CancellationToken cancellationToken = default);
    Task<PreparedCodeGraphOwnershipTransitionResult> CommitCreatedAsync(
        PreparedCodeGraphWorkspaceOwnership creating,
        PreparedToolDirectoryObjectIdentity createdEntry,
        CancellationToken cancellationToken = default);
    Task<PreparedCodeGraphOwnershipTransitionResult> BeginDispositionAsync(
        PreparedCodeGraphWorkspaceOwnership ready,
        CancellationToken cancellationToken = default);
    Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
        PreparedCodeGraphWorkspaceOwnership disposing,
        CancellationToken cancellationToken = default);
    Task<PreparedCodeGraphOwnershipTransitionResult> QuarantineAsync(
        PreparedCodeGraphWorkspaceOwnership current,
        CancellationToken cancellationToken = default);
}

internal sealed class PreparedCodeGraphStateCoordinator
{
    internal PreparedCodeGraphStateCoordinator(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        IPreparedCodeGraphWorkspaceOwnershipVerifier workspaceOwnershipVerifier,
        IPreparedCodeGraphOwnershipStore ownershipStore);
    internal Task<PreparedCodeGraphOwnershipViewResult> GetOwnershipViewAsync(
        PreparedCodeGraphWorkspaceOwnership workspaceBinding,
        PreparedSourceApproval currentApproval,
        CancellationToken cancellationToken = default);
    internal Task<PreparedToolWorkspaceCheckStatus> UseOwnedStateAsync(
        PreparedToolWorkspaceLease codeGraphLease,
        PreparedCodeGraphWorkspaceOwnership workspaceOwnership,
        Func<PreparedCodeGraphStateScope, CancellationToken, ValueTask> use,
        CancellationToken cancellationToken = default);
    internal Task<PreparedToolWorkspaceCheckStatus> DisposeOwnedStateAsync(
        PreparedSourceApproval dispositionApproval,
        PreparedCodeGraphWorkspaceOwnership workspaceOwnership,
        CancellationToken cancellationToken = default);
}
internal interface IPreparedCodeGraphWorkspaceOwnershipVerifier
{
    // Trusted binding-source verification, never a Worker argument.
    // Checks the exact dedicated workspace and owner record for each use/disposition.
    ValueTask<bool> VerifyCurrentAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken cancellationToken = default);
    // The registry confirms closure and actual registered resources settled.
    ValueTask<bool> VerifyDispositionReadyAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken cancellationToken = default);
}
internal sealed class PreparedCodeGraphStateScope
{
    internal string GetPhysicalStatePathForTrustedToolBinding();
    // Any application file operations use held-root-relative, no-follow descendants.
}
```

The trusted workspace owner supplies an `Absent` draft with a new `OwnershipId`, version zero, exact approved workspace/root/owner fields and null entry identity; no Worker/tool can construct or select it. `BeginCreateAsync` atomically inserts that draft as a **durable `Creating` record** only if no record for the ID/workspace exists. Every later store transition compares the expected ID/version/phase and persists its new version before returning `Committed` plus that new record; `Conflict` or `Unavailable` never returns a usable binding. The record lives in application-owned metadata outside `.codegraph`; trusted application access checks keep its bytes unavailable through ordinary model-visible Worker tools. The fixed `SourceStorage/.prepared-codegraph-ownership` file-CAS store now exists; production trusted binding, selected-host placement/permissions and native settlement remain unqualified. Under the user's direct-child exception this placement does **not** prove the pinned native CodeGraph process lacks OS read/write access to those bytes; that risk must be reported and actual mutations observed by Story 18.3. If no protected production store is configured, creation and verified reads are unavailable. The only accepted state-entry name is the coordinator's fixed `.codegraph` constant, not a record or call argument. Object identities intentionally omit mutable directory last-write time.

The coordinator is the sole transition producer. After `BeginCreateAsync` commits `Creating`, it confirms the entry is absent, exclusively creates `.codegraph` through the held root, observes its stable physical identity, rechecks exact source/tool/root/owner authority, and calls `CommitCreatedAsync(creating, observedEntry)`. Only a **durably committed `Ready` record with that non-null identity** may be returned by `GetOwnershipViewAsync` or used to invoke the adapter callback. A failed exclusive create, observed identity mismatch, authority loss, cancelled/failed `CommitCreatedAsync`, or uncertain persistence moves the workspace to `Quarantined` when possible; an unconfirmed `Creating` record remains unavailable after restart and requires explicit owner resolution. Never adopt a preexisting entry by name or let a null-identity draft pass a post-create check. The owner verifier confirms dedicated workspace ownership and exact grant/root independently of record field equality.

`GetOwnershipViewAsync` is the one internal producer of current ownership classification for later leases and Story 18.3's reader. `workspaceBinding` is the application-held exact owner/root draft or record, never a Worker argument. The store's `LoadAsync` distinguishes a current `Found` record from **positively verified absence for that same workspace key** and `Unavailable`; a null or thrown load is never interpreted as absence. The coordinator matches trusted current approval, freshly calls `IPreparedSourceCurrentAuthorityVerifier`, calls `IPreparedCodeGraphWorkspaceOwnershipVerifier.VerifyCurrentAsync` for the exact binding and observes the root/entry objects under held no-follow handles. `VerifiedOwned` contains the **same typed current `Ready` record** with non-null entry identity, after matching expected ownership ID, owner/root/approval and verifying the *loaded* version is current; an older caller copy is replaced only by this checked view, never used directly. `VerifiedUnowned` contains no positive owned record and is possible only for a verified `Absent` draft, `LoadStatus.VerifiedAbsent`, current source/workspace-owner verification and unchanged root object; a same-name source entry alone never proves ownership. A conflicting owner/root/approval, `Creating`, `Disposing`, `Removed` with a present entry, `Quarantined`, missing/unavailable store, changed entry or unavailable source/owner authority yields `Unavailable` and no record. Story 18.3 consumes these states in its reader repair: it excludes only verified owned state, retains verified unowned source, and fails closed on unavailable ownership rather than exposing cache or hiding source by name. CodeGraph preparation/use additionally requires the separate current `CodeGraphPreparation` tool authorizer and a current lease; reader classification does not treat that tool-purpose grant as the source of ownership. No second descriptor or filter contract is introduced.

`UseOwnedStateAsync` checks a current `CodeGraphPreparation` lease, exact approval/tool authority, the current positive ownership record and pinned root object identity before and after its bounded callback. For an `Absent` draft it performs the durable `Creating`/exclusive-create/`Ready` handoff above before invoking the callback; for a later `Ready` record it reloads and requires `GetOwnershipViewAsync` to return `VerifiedOwned` with the same current version and physical entry identity. It never returns `Current` or exposes a state scope while the record is `Creating` or its commit is uncertain. Creation changes the prepared-root directory last-write time; the current `PreparedSourceTraversal.DirectoryIdentity` includes that mutable field, so the CodeGraph state operation must compare stable root object identity while still rejecting root replacement. It must not treat expected cache-entry metadata changes as proof of source mutation. JDT's separate-state validation is unchanged; Story 18.3 owns the ordinary reader's corresponding in-place state handling. Any direct application descendant create/open/modify operation is handle-relative with no-follow traversal and verifies ownership/current authority before mutation; symlinks/reparse substitutes, hardlink aliases to source, traversal, siblings and another state name are denied. A copied trusted child binding is revalidated immediately before launch. The callback does not encompass an unbounded native MCP session; the adapter checks live authority during use and before disclosure. A post-check failure cannot report success and closes the lease. If mutation may have occurred at authority loss, quarantine the workspace; no ordinary retry adopts or deletes the entry.

`DisposeOwnedStateAsync` is invoked by trusted application composition only at workspace disposition after stopping new grants and settling CodeGraph children/handles. `dispositionApproval` is the currently selected, application-held approval, never a Worker argument; it must exactly equal the approval in the current durable `Ready` record. The coordinator's injected *existing* `IPreparedSourceCurrentAuthorityVerifier` must return `Current` with `CurrentApproval == dispositionApproval`, and its injected *existing* `IPreparedToolUseAuthorizer` must return `Current` for that approval and the hard-coded `CodeGraphPreparation` purpose. The ownership verifier's disposition-ready check must confirm stopped grants and settled child/handles from the trusted workspace/process owners; a caller assertion alone is insufficient. The coordinator atomically persists `BeginDispositionAsync(ready)` to `Disposing` **before** removing any entry, so new verified views and uses stop. Before each bounded cleanup chunk it checks fresh source/tool/owner/readiness results, the durable `Disposing` version, and reopened held no-follow root plus recorded entry identity. After removal it repeats the fresh authority/owner/readiness and root-object checks and verifies absence of the recorded entry; there is no removed entry identity to compare. Only then may `CommitRemovedAsync(disposing)` durably change the same record to `Removed`, and the coordinator reads back that terminal version before reporting success. A substituted entry is never deleted by name. Neither the record nor a previously granted or revoked lease supplies current source/tool authority. A denied, changed, unavailable, cancelled or timed-out check prevents further deletion and cannot report success. If cleanup has begun, if a child/handle is not demonstrably settled, or if the post-check, `CommitRemovedAsync` or readback fails, the workspace is quarantined and unavailable for reuse; a surviving `Disposing` version after restart is also unavailable and requires explicit owner resolution, never a by-name retry. `QuarantineAsync` is a durable compare-and-set transition when the store is reachable; an unavailable store itself denies all verified views and grants until resolved. The coordinator removes only the recorded `.codegraph` entry and descendants through held-root-relative no-follow operations, never source or siblings. A resumed workspace with lost/unverifiable record is quarantined rather than adopted by name. This contract does not prescribe a new database schema or general-purpose workspace-write API.

For Story 18.3's returned source excerpts, the integrated `IPreparedSourceReadContext.ReadTextRangeAsync` supplies bounded held-root-relative no-follow source reads, but its current root metadata and traversal behavior are not yet qualified for in-place cache writes. After Story 18.3's reader correction, trusted composition may use it only under the same exact approval/root binding as the current `CodeGraphPreparation` lease, after mapping the native location to a contained relative path; compare the bounded bytes and treat `Limited`, `ChangedInput`, denied or unavailable reads as no verified excerpt. Recheck both source-read and CodeGraph-purpose authority before disclosure. This reuses Story 18.1's public read contract; Story 18.3 owns the reader integration and continuation repair, and 18.2 adds no second source-reader/filter contract. The positive owner binding needed to exclude cache from ordinary reading is an internal verified producer output, not a lexical `.codegraph` rule or a Worker-visible handle.

These controlled operations prove application-side scope and ownership only. Under the user's narrow direct-process exception, a pinned trusted CodeGraph child inherits host filesystem rights and is not constrained by this C# callback, state path or ownership store; no preventive OS filesystem or network denial is claimed. Story 18.3 must observe actual native writes, block readiness if anything outside the owned `.codegraph` subtree changes or source/configuration/hooks change, report remaining external-access and network uncertainty, and qualify native MCP and reader/continuation behavior under separate execution permission. Story 18.2 owns this shared seam and focused application tests. Story 18.3 owns adapter/index/session code and `PreparedSourceReadContext.cs`/agreed traversal repair, with 18.8/18.12 downstream. The changed seam requires new independent plan/definition reviews and applicable bound approvals before implementation or 18.3 consumption.

## Navigation context and Worker functions

Story 18.2's Core interface is a small borrowed capability; its Infrastructure implementation owns JDT-LS. Intended callable surface:

```csharp
public interface IPreparedJavaNavigationContext : IAsyncDisposable
{
    Guid RepositoryId { get; }
    Task<PreparedJavaNavigationResult> GetDefinitionAsync(
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default);
    Task<PreparedJavaNavigationResult> GetReferencesAsync(
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default);
}
public sealed record PreparedJavaNavigationRequest(
    string RelativePath, int Line, int Character, int MaximumLocations);
public sealed record PreparedJavaNavigationResult(
    Guid RepositoryId, PreparedJavaNavigationOperation Operation,
    PreparedJavaNavigationOutcome Outcome,
    IReadOnlyList<PreparedJavaLocation> Locations,
    IReadOnlyList<PreparedJavaLimitation> Limitations,
    PreparedJavaStoppingLimit? StoppingLimit);
public sealed record PreparedJavaLocation(
    PreparedSourceLocation Source, int StartLine, int StartCharacter,
    int EndLine, int EndCharacter);
public enum PreparedJavaNavigationOperation { Definition, References }
public enum PreparedJavaNavigationOutcome
{
    Observed, NoResultObserved, Limited, Denied, AuthorityUnavailable,
    ChangedInput, ConfigurationUnavailable, ReadinessFailed,
    ProtocolFailed, Cancelled, DeadlineExceeded
}
public sealed record PreparedJavaLimitation(string Code, PreparedSourceLocation? Source);
public enum PreparedJavaStoppingLimit { LocationLimit, ResponseBytes, Deadline }

public sealed class PreparedJavaNavigationFunctions
{
    public PreparedJavaNavigationFunctions(IPreparedJavaNavigationContext context);
    public IReadOnlyList<Microsoft.Extensions.AI.AIFunction> Create();
}
```

`Create()` returns exactly `navigate_java_definition` and `navigate_java_references`, each borrowing the same already-issued context. Worker arguments are only canonical Repository-relative Java path, one-based position and bounded maximum locations. They cannot select another batch, Repository, root, purpose, executable, adapter, import/build mode or approval. The trusted composition caller owns context/lease disposal after calls settle. Per-call cancellation is forwarded and cannot cause a cached late result to be released.

Results echo only `repositoryId`, operation, sanitized outcome, contained relative locations/ranges, explicit result bound and safe limitation codes. No raw physical URI, source text, subject, approval identity, tool-state path, outside-root metadata, stack trace or exception text. Zero locations mean `no result observed`, never proof of absence. A capped response reports `limited`; there is no implied continuation or full coverage. Outside-root/unmappable URIs are withheld with a limitation. Revocation, changed authority, unavailable verifier, cancellation or disposal returns an empty sanitized payload after discarding all buffered locations and counts.

## October Infrastructure construction seam

The existing `JdtLsAnalysisSessionFactory.OpenAsync(AnalysisScope)` uses a revision-bound workspace and cannot open this prepared-root context. Trusted composition uses one October-specific factory in `Vulgata.Infrastructure.Analysis.Java`. The Core/Agent interface above remains unchanged. The callable shape below fixes the construction seam for controlled process/protocol tests and later production composition; it is not a new Worker operation:

```csharp
public sealed record PreparedJavaNavigationProfile(
    string DisposableStateRoot,
    TimeSpan InitializationTimeout,
    TimeSpan RequestTimeout,
    TimeSpan ShutdownTimeout,
    int MaximumResponseBytes);
public enum PreparedJavaNavigationOpenStatus
{
    Opened, Denied, AuthorityUnavailable, RootUnavailable,
    ConfigurationUnavailable, ReadinessFailed, Cancelled, DeadlineExceeded,
    CleanupUnconfirmed
}
public sealed record PreparedJavaNavigationOpenResult(
    PreparedJavaNavigationOpenStatus Status,
    IPreparedJavaNavigationContext? Context);

public sealed class PreparedJavaNavigationContextFactory
{
    public PreparedJavaNavigationContextFactory(
        PreparedJavaNavigationProfile profile,
        IPreparedJdtLsRuntime runtime);
    internal PreparedJavaNavigationContextFactory(
        PreparedJavaNavigationProfile profile,
        IPreparedJdtLsRuntime runtime,
        PreparedCodeGraphStateCoordinator ownershipCoordinator,
        Func<PreparedToolWorkspaceLease, CancellationToken,
            ValueTask<PreparedCodeGraphWorkspaceOwnership?>> currentWorkspaceBinding);
    public Task<PreparedJavaNavigationOpenResult> OpenAsync(
        PreparedToolWorkspaceLease workspaceLease,
        CancellationToken cancellationToken = default);
}

// Trusted Infrastructure/test adapter seam, never a Worker function or model input.
// OpenAsync owns all resources it acquires until it successfully returns a session.
// On throw/cancellation before return, it settles them itself; no partial handle
// exists for the caller to dispose.
public interface IPreparedJdtLsRuntime
{
    Task<IPreparedJdtLsSession> OpenAsync(
        PreparedToolWorkspaceLease workspaceLease,
        PreparedJavaNavigationProfile profile,
        CancellationToken cancellationToken = default);
}
// Infrastructure-only production construction, selected by trusted composition.
internal sealed class PreparedJdtLsRuntime : IPreparedJdtLsRuntime
{
    internal PreparedJdtLsRuntime(
        IPreparedJdtCommandResolver commands,
        IPreparedJdtProcessLauncher launcher,
        IPreparedJdtProtocolFactory protocols,
        PreparedJdtHostPolicy fixedHostPolicy);
    public Task<IPreparedJdtLsSession> OpenAsync(
        PreparedToolWorkspaceLease workspaceLease,
        PreparedJavaNavigationProfile profile,
        CancellationToken cancellationToken = default);
}
internal interface IPreparedJdtCommandResolver
{
    Task<PreparedJdtCommandResolution> ResolveAsync(
        PreparedJdtCommandRequest request,
        CancellationToken cancellationToken);
}
internal sealed record PreparedJdtCommandRequest(
    string HostPathSnapshot, string PreparedRoot, string DisposableStateRoot,
    PreparedJdtHostPolicy FixedHostPolicy);
internal enum PreparedJdtCommandResolutionStatus { Pinned, Missing, Rejected, Unavailable }
internal sealed record PreparedJdtCommandResolution(
    PreparedJdtCommandResolutionStatus Status,
    PreparedJdtPinnedCommands? Commands);
internal sealed record PreparedJdtPinnedCommands(
    PreparedJdtPinnedFile JdtCommand, PreparedJdtPinnedFile JavaCommand,
    PreparedJdtPinnedFile? FixedInterpreter);
internal sealed record PreparedJdtFileIdentity(
    uint VolumeSerialNumber, uint FileIndexHigh, uint FileIndexLow);
internal sealed record PreparedJdtPinnedFile(
    string CanonicalAbsolutePath, PreparedJdtFileIdentity FileIdentity,
    string Sha256, int? FirstPathEntryIndex);
internal sealed record PreparedJdtHostPolicy(
    IReadOnlyList<string> ApprovedExecutableDirectories,
    IReadOnlyList<string> DeclaredRuntimeReadRoots,
    string FixedCmdAbsolutePath,
    IReadOnlyList<string> FixedChildPathEntries);
internal interface IPreparedJdtProcessLauncher
{
    Task<IJdtLsProcess> LaunchAsync(
        PreparedToolWorkspaceLease workspaceLease,
        PreparedJdtLaunchRequest request,
        CancellationToken cancellationToken);
}
internal sealed record PreparedJdtLaunchRequest(
    string CanonicalPreparedRoot, PreparedToolDirectoryObjectIdentity RootIdentity,
    string RootUri, string DataPath, string ConfigurationPath,
    PreparedJdtPinnedCommands Commands, IReadOnlyList<string> FixedArguments,
    IReadOnlyDictionary<string, string> FixedChildEnvironment,
    PreparedJdtHostPolicy FixedHostPolicy);
internal interface IPreparedJdtProtocolFactory
{
    Task<IPreparedJdtLsSession> InitializeAsync(
        IJdtLsProcess process,
        PreparedJdtInitializeRequest request,
        CancellationToken cancellationToken);
}
internal sealed record PreparedJdtInitializeRequest(
    string RootUri, string WorkspaceFolderUri, string WorkspaceFolderName,
    IReadOnlyDictionary<string, object> FixedSettings, int MaximumResponseBytes);
public sealed class PreparedJdtLsOpenFailureException : Exception
{
    public PreparedJdtLsOpenFailureException(
        PreparedJavaNavigationOpenStatus status, bool cleanupConfirmed);
    public PreparedJavaNavigationOpenStatus Status { get; }
    public bool CleanupConfirmed { get; }
}
public interface IPreparedJdtLsSession : IAsyncDisposable
{
    Task<PreparedJdtLsRawReply> NavigateAsync(
        PreparedJavaNavigationOperation operation,
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default);
}
public sealed record PreparedJdtLsRawReply(
    IReadOnlyList<PreparedJdtLsRawLocation> Locations,
    bool LimitReached,
    string? CapabilityLimitation);
public sealed record PreparedJdtLsRawLocation(
    string RawProtocolUri, int StartLine, int StartCharacter, int EndLine, int EndCharacter);
```

The internal types above are an October-only construction seam, not an Agent/Worker surface. The runtime reads the running process's host PATH into `HostPathSnapshot`; Worker input never supplies it. `PreparedJdtHostPolicy` is fixed by trusted application composition; it lists directories to *validate* PATH candidates and declared runtime read inputs, never a fallback executable selector. `PreparedJdtCommandResolution` is `Pinned` with all required descriptors or `Missing`, `Rejected` or `Unavailable` with null commands; it never resolves a second PATH match after rejecting the first. `FirstPathEntryIndex` is non-null for the first matching JDT and Java entries independently; a fixed `cmd.exe` is checked outside PATH and has a null index. Pinning covers canonical absolute paths, stable physical file identities and content digests, with held protection against replacement where available. Invalid or changing identity returns no launch. `IJdtLsProcess` is the existing Infrastructure-only process/stdio/settlement shape; the new launcher owns an OS-enforced child and is not the old `ProcessJdtLsLauncher` implementation.

The application constructs `PreparedJavaNavigationProfile` from trusted bounds and a disposable state root outside the prepared source; it contains no executable selector. The caller first obtains a `PreparedToolWorkspaceLease` with `JdtNavigation` purpose. The internal factory constructor binds the **existing** `PreparedCodeGraphStateCoordinator` and an Infrastructure-only callback that obtains the current exact `PreparedCodeGraphWorkspaceOwnership` binding from the trusted application workspace owner. The callback is not a second ownership view or a Worker-selected descriptor. The factory checks `lease.MatchesApproval(binding.Approval)`, root/owner binding and current authority under its held scope; it passes that same application-held binding and approval to `GetOwnershipViewAsync`. A null/mismatched binding, missing coordinator/store or `Unavailable` view leaves navigation `ConfigurationUnavailable` with no runtime or protocol call. The public two-argument constructor remains source compatible for earlier controlled lifecycle tests, but cannot supply ownership eligibility and cannot enable a production navigation context by itself. Trusted Web composition registers only the bound factory; a fake runtime is not a substitute for the binding. Factory `OpenAsync` also checks purpose, lease lifetime/current authority, root identity and profile/state separation and reserves the one-session-per-Repository slot before calling the injected runtime. The runtime obtains the exact whole-root path/object identity only within the short held-root callback and copies it into `PreparedJdtLaunchRequest` for trusted use. The launcher re-enters the current lease/root check and rechecks every pinned command immediately before OS creation, then applies the restricted child identity and owned process-tree lifetime before executable code runs. It returns a process only after those steps succeed; a caller bool or recorded request is not proof. It passes the fixed interpreter, script, Java and allowed descendants a constrained child `PATH`, minimal inherited stdio handles and one OS restriction boundary. `Opened` has exactly one context; every other status has null context and no model-visible trusted details. Null programmer inputs fail without launch.

For every navigation request, the context reloads that same ownership view **before** protocol use. For a candidate whose first relative component is the coordinator's fixed `.codegraph` entry, the existing held-root scope observes that physical entry identity under no-follow traversal; the component only selects the identity check and is never by itself an exclusion rule. `VerifiedOwned` with a current `Ready` record excludes the input only when the observed entry identity equals the record's physical state-entry object; `VerifiedUnowned` admits ordinary source even when a directory is named `.codegraph`. The context maps each raw returned URI through the existing held-root mapper, then applies the same contained first-component/physical-identity check within the bounded root callback, withholding only locations beneath the verified current owned object. It reloads the view immediately before Worker disclosure and requires the status and, for `VerifiedOwned`, `OwnershipId`, version and physical entry identity to remain current; transition, substituted entry, cancellation, authority loss or `Unavailable` discards all buffered locations, counts and limit details. If `VerifiedUnowned` changes to owned during a request, discard the full response and reissue only under a newly checked request; do not infer absence from an empty result. Ownership classification does not require a `CodeGraphPreparation` grant: the JDT operation continues to require its own `JdtNavigation` purpose. This changes neither 18.1's reader nor 18.3's separately owned list/read/search repair.

The initialization request uses the same granted root for `rootUri` and the single `workspaceFolders[0].uri`, with name `repository`; neither `-data` nor configuration nor temporary/profile data may overlap the prepared root. The fixed settings map contains `java.import.maven.enabled=false`, `java.import.gradle.enabled=false`, `java.autobuild.enabled=false`, `java.configuration.updateBuildConfiguration="disabled"`, `java.import.maven.offline.enabled=true`, `java.import.gradle.offline.enabled=true`, `java.import.gradle.wrapper.enabled=false`, `java.maven.downloadSources=false` and `java.maven.updateSnapshots=false`. The proposed wire shape nests their `java` members in `initialize.initializationOptions.settings`; after `initialized`, send the identical `workspace/didChangeConfiguration.settings` before reporting `Opened` if the pinned server requires that notification. Neither stage may reenable build/import. These names are candidate client/server settings documented by the JDT/Java extension sources; T022 must observe the serialized request/notification order and T023 must verify the pinned 1.61.0 server accepts and honors this exact shape, failing closed if it does not. T024 must observe effective absence of import/build/restore and source writes, since serializing booleans does not establish it.

For the declared Windows host, a Less Privileged AppContainer (LPAC) launched with a package/capability identity and an All Application Packages opt-out is one candidate for the required real boundary. Its package/capability grants must allow only read/execute on pinned `cmd.exe`, the selected JDT script, Java and transitive runtime inputs, read on the exact prepared root, and write on bounded disposable JDT state. LPAC redirects profile and TEMP/TMP data: those locations must be owned, inspected and cleaned as tool state, or the profile fails. A job object may bound the child tree but is not a filesystem permission. AppContainer creation, effective DACLs for shared installations, fixed interpreter and all Python/Java descendants, and actual unrelated-host denial are unresolved host prerequisites; no shared ACL or PATH edit, child launch or production registration follows from this candidate. If the first PATH script requires an unapproved Python/external-checkout chain, it is rejected rather than adopted or skipped for a later entry.

Ownership has three distinct phases. **Before runtime return**, `IPreparedJdtLsRuntime.OpenAsync` owns every process, protocol and disposable-state resource it starts. `IPreparedJdtProtocolFactory.InitializeAsync` may wrap the process but does not transfer its cleanup obligation to the caller; on failed or cancelled initialization the runtime settles both process and protocol. If opening fails or is cancelled before returning `IPreparedJdtLsSession`, it performs bounded cleanup, then throws `PreparedJdtLsOpenFailureException` with a sanitized status and whether cleanup was confirmed; the factory cannot dispose handles it never received. A confirmed failure lets the factory release its slot and return that sanitized status with null context. An unconfirmed failure (or unexpected exception without cleanup attestation) maps to `CleanupUnconfirmed` with null context, keeps the slot unavailable and forbids retry until owned-resource resolution; it cannot be reported as safe `Cancelled`. A second open for the same Repository while quarantined returns `CleanupUnconfirmed` without calling the runtime. If this trusted status must be surfaced through a Worker function, map it to existing `PreparedJavaNavigationOutcome.ConfigurationUnavailable` with empty locations, counts and details. The exception never carries raw paths/source/approval or protocol details into the Worker result. **After session return but before context handoff**, the factory owns the returned session. If cancellation, a current-lease/profile check or context construction then fails, it disposes that session before mapping a sanitized null-context status and releasing its session slot. **After successful context handoff**, the context owns and disposes the returned session and releases the slot after Worker calls settle. The context borrows the separately issued lease; the trusted caller disposes the lease after the context, or after a failed open. The factory never disposes that caller-owned lease. A failed/disposed context or partial runtime session cannot be reused.

The injected `IPreparedJdtLsRuntime` is selected only by trusted Infrastructure composition. A controlled fake may report raw protocol replies and record its own pre-return cleanup or a returned session's later disposal for T010/T011/T013/T018 without starting Java or JDT-LS. Its `RawProtocolUri` is the JSON-decoded location field before `System.Uri` construction, so actual Windows device spellings can reach the denial seam and be tested. The context validates and maps those raw strings to the granted relative root, performs current-authority/pre-disclosure checks and sanitizes outcomes; the fake cannot turn raw locations into authorized output by itself. Fake runtime behavior cannot prove real `rootUri`, `-data`/configuration placement or fixed JDT settings; those require observable production-runtime checks and T022–T024 qualification. Production composition must remain unregistered/fail closed for a real runtime until accepted T022/T023 trusted PATH and process-profile evidence. The eventual real runtime reuses assessed stdio/StreamJsonRpc/process mechanics behind this interface, never the legacy `AnalysisScope` factory or revision lease. No caller passes a model-selected root, command, adapter or physical workspace through `OpenAsync`.

### Production binding and shared-use handoff

The current checkout implements `PreparedToolWorkspaceIssuer`, `PreparedCodeGraphStateCoordinator`, the internal typed ownership record/view and `FilePreparedCodeGraphOwnershipStore` with the fixed `SourceStorage/.prepared-codegraph-ownership` ledger/lock. `Program.cs` registers an optional `PreparedJavaWorkerToolComposer`; it does not register a trusted source/tool verifier, dedicated-workspace binding producer, owner/readiness verifier or protected production store. A test-created `PreparedCodeGraphWorkspaceOwnership` is input evidence for the controlled coordinator, not production authority. The following small composition is the Story 18.2 proposal, not an implemented or enabled service.

```csharp
internal enum PreparedManagedWorkspaceBindingStatus { Bound, Denied, Unavailable }
internal sealed record PreparedManagedWorkspaceBindingResult(
    PreparedManagedWorkspaceBindingStatus Status,
    PreparedCodeGraphWorkspaceOwnership? Binding);

internal sealed class PreparedManagedWorkspaceBindingSource
{
    internal PreparedManagedWorkspaceBindingSource(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        FilePreparedCodeGraphOwnershipStore ownershipStore,
        IPreparedManagedWorkspaceHostVerifier hostProtection,
        PreparedWorkspaceUseRegistry uses);
    internal Task<PreparedManagedWorkspaceBindingResult> BindAsync(
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        PreparedToolPurpose selectedPurpose,
        CancellationToken cancellationToken = default);
}

internal enum PreparedManagedWorkspaceHostStatus
{
    Observed, Protected, Denied, Unavailable
}
internal sealed record PreparedManagedWorkspaceHostObservation(
    PreparedManagedWorkspaceHostStatus Status,
    PreparedToolDirectoryObjectIdentity? RootObjectIdentity,
    string? DerivedWorkspaceOwnerIdentity);
internal sealed record PreparedManagedWorkspaceHostHoldResult(
    PreparedManagedWorkspaceHostStatus Status,
    IPreparedManagedWorkspaceHostHold? Hold);
internal interface IPreparedManagedWorkspaceHostHold : IAsyncDisposable
{
    ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken = default);
}
internal interface IPreparedManagedWorkspaceHostVerifier
{
    // Observation derives fields for lookup; it grants no use or mutex ownership.
    Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default);
    // Rechecks the exact binding and returns a held root/metadata/mutex proof.
    Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default);
}
// The selected Windows adapter receives the approved principal from trusted host
// configuration, checks it against the effective process token and OS objects,
// and never accepts that principal through a Worker/tool call.
internal sealed class WindowsPreparedManagedWorkspaceHostVerifier
    : IPreparedManagedWorkspaceHostVerifier
{
    internal WindowsPreparedManagedWorkspaceHostVerifier(
        System.Security.Principal.SecurityIdentifier approvedApplicationPrincipal);
    public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default);
    public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default);
}

internal enum PreparedWorkspaceSettlementStatus { Settled, Active, Unknown }
internal interface IPreparedWorkspaceResourceSettlementProbe
{
    ValueTask<PreparedWorkspaceSettlementStatus> InspectOwnedResourcesAsync(
        CancellationToken cancellationToken = default);
}
internal interface IPreparedWorkspacePreviousGenerationVerifier
{
    // Only an actual native owner can establish old child/handle settlement.
    ValueTask<PreparedWorkspaceSettlementStatus> VerifySettledAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken = default);
}
internal enum PreparedWorkspaceAdmissionStatus { Opened, Denied, Unavailable, Cancelled }
internal sealed record PreparedWorkspaceAdmissionResult(
    PreparedWorkspaceAdmissionStatus Status);
internal sealed class PreparedWorkspaceUseRegistry : IAsyncDisposable
{
    internal PreparedWorkspaceUseRegistry(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        IPreparedCodeGraphOwnershipStore ownershipStore,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        IPreparedManagedWorkspaceHostVerifier hostProtection,
        IPreparedWorkspacePreviousGenerationVerifier previousGeneration,
        TimeSpan admissionDeadline,
        TimeSpan dispositionDeadline);
    internal Task<PreparedWorkspaceAdmissionResult> AdmitAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        PreparedToolPurpose selectedPurpose,
        CancellationToken cancellationToken = default);
    internal Task<PreparedWorkspaceBorrowResult> BeginBorrowAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        PreparedToolPurpose purpose,
        IPreparedWorkspaceResourceSettlementProbe resources,
        CancellationToken cancellationToken = default);
    internal Task<PreparedWorkspaceSettlementStatus> CloseForDispositionAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
}
internal sealed record PreparedWorkspaceBorrowResult(
    PreparedWorkspaceSettlementStatus Status,
    PreparedWorkspaceBorrow? Borrow);
internal sealed class PreparedWorkspaceBorrow : IAsyncDisposable
{
    // Dispose only after the registered probe has verified process, protocol,
    // active calls and held handles settled; otherwise the registry stays Unknown.
    public ValueTask DisposeAsync();
}
```

`PreparedWorkspaceUseRegistry` has one admitted key per instance and starts Closed. T031 constructs it with controlled source/tool/store/host/previous-generation dependencies and calls `AdmitAsync` directly on a controlled existing-record fixture before `BeginBorrowAsync`; T033's later binding source is not needed to create this test state. The controlled host fake returns a scoped hold for call-order, race and disposal tests; this is not selected-host ACL/mutex qualification. A production host verifier is registered only with an approved principal from trusted host configuration and checks that identity against the effective process token, no-follow root/metadata objects and protected named mutex. The approved principal is not a Worker/tool argument. `ObserveAsync` returns `Observed` with non-null root/derived-owner fields for lookup but grants no use; `VerifyAndHoldAsync` returns `Protected` only with one hold that owns the root/metadata handles and mutex. A `Protected` status with null hold, or `Observed` on the hold path, is `Unavailable`; a held scope's `CheckCurrentAsync` must continue returning `Protected` for the same root/owner/metadata/mutex before use and disposition. A failed check latches the registry unavailable, stops issue/borrow and cannot authorize cleanup. Only the registry owns and disposes a successful hold. Observation and hold objects are process-local proof, never a second serialized ownership descriptor or reader classification.

`AdmitAsync(binding, selectedPurpose, token)` accepts an internal `Absent` draft only with the fixed initialized ledger's positive `VerifiedAbsent`, or a current `Ready` record exactly reloaded by the same store; transitional/terminal phases and stale versions stay Closed. The trusted constructor's positive `admissionDeadline` bounds source/tool/store/host/mutex/previous-generation checks, and its positive `dispositionDeadline` bounds closure; neither is a Worker argument. It verifies current exact source approval and selected tool purpose, then obtains and checks the selected-host hold. A fresh-process `Ready` additionally requires `IPreparedWorkspacePreviousGenerationVerifier.VerifySettledAsync` to return `Settled` from independent old child/handle evidence. Missing provider, `Active`/`Unknown`, empty local tracking, mutex reacquisition or parent PID exit cannot satisfy this check. After a final source/tool/store/hold check under the registry gate, it publishes `Opened`; every other result has no borrow/lease and disposes any partial hold. Cancellation before publish returns `Cancelled` and stays Closed; timeout, exception or uncertain cleanup returns `Unavailable`. Neither a later restored verifier nor a second `AdmitAsync` may reopen a Closing, Unknown or disposed instance. The status is trusted local output, not a Worker grant.

The admitted key is immutable approval, physical root object, derived owner and `OwnershipId`; phase/version are reloaded from the same store for current use. A same-process `Absent` → `Creating` → `Ready` progression under the coordinator may advance the current version for that key while the registry retains its host hold; the registry requires exact monotonic store readback, never a copied Ready or changed immutable field. A new process has no retained hold/use history and treats persisted Ready as old-generation Unknown until separately settled. `BeginBorrowAsync` and the tracked issuer verify the Open state, current source/tool purpose, current store version and held proof before registering resources. A JDT borrow may use positively Absent or current Ready ownership classification; a native CodeGraph borrow requires current Ready, while a tracked CodeGraph-purpose lease may be issued under Absent solely for coordinator preparation. Creating/Disposing/uncertain versions permit no new native borrow. `CloseForDispositionAsync` latches new grants closed before checking existing tickets and returns `Settled` only after all same-key resources and the host hold remain verified; it does not revoke the underlying current `CodeGraphPreparation` authorization, which the coordinator still needs for fresh checked cleanup. The registry owns its hold through `Removed` readback or quarantine. Its `DisposeAsync` closes admissions, bounds waiting for owned uses, disposes the hold and records unknown settlement if resources cannot be proved closed; process exit is not a cleanup receipt.

`PreparedManagedWorkspaceBindingSource.BindAsync` first calls `ObserveAsync` to derive root identity and owner for `FindExactWorkspaceAsync`, then rechecks a found record through `LoadAsync` or constructs a positively absent draft. It calls the registry's `AdmitAsync` with that binding and selected purpose; only `Opened` plus a final exact record/root check yields `Bound`. `VerifyAndHoldAsync` rechecks observation under the held root/mutex, so a race between observation and admission returns `Unavailable`. A T031 controlled fixture can call the registry directly with a manually built internal binding; production never treats that fixture, a caller `dedicated` boolean or owner string as authority. Actual Windows host protection and real previous-generation settlement remain separate qualification.

`BindAsync` is callable only from trusted application admission with the exact grant used for 18.1 issuance; its physical root, approval and purpose are never Worker arguments. It rechecks `IPreparedSourceCurrentAuthorityVerifier` and `IPreparedToolUseAuthorizer`, opens the root without following substitutions, and compares the held root object identity. The host verifier is a concrete selected-host adapter, not an injected allow boolean: it checks that this operator-admitted root is the dedicated Vulgata-managed workspace under the approved application principal, derives workspace-unique `WorkspaceOwnerIdentity` from that principal plus the observed root object (not caller text), and validates no-follow source/metadata separation and the fixed SourceStorage child. Ordinary Worker tools must be unable to name/traverse that separate metadata root through their granted APIs; host ACL/mount protection against unrelated principals also needs qualification. Workers in the same app process may share its OS principal, so no false distinct-identity or preventive pinned-child claim follows. For single active host ownership, select a process-lifetime OS named mutex whose name is derived from a digest of the observed root object and approved application principal, with an app-only security descriptor; hold it through the binding/registry lifetime and disposition. Failure to acquire or qualify it returns `Unavailable`, rather than treating short file-CAS locks as a host lease. A crash releases that mutex but cannot prove prior native resources settled. The app's effective principal and host ACL/mount behavior require direct qualification. The trusted pinned CodeGraph child may inherit that principal's rights, so metadata placement makes no claim of denying that child OS access; its observed effects are Story 18.3 work.

The binding source constructs only the existing record shape. Existing `IPreparedCodeGraphOwnershipStore.LoadAsync(expectedBinding)` cannot discover a prior `OwnershipId` from a newly constructed absent draft: it returns `Unavailable` if an exact workspace exists with another ID. Add one internal `FindExactWorkspaceAsync(approval, physicalRootPath, rootObjectIdentity, derivedWorkspaceOwnerIdentity, cancellationToken)` lookup to that same store/interface, returning the existing `PreparedCodeGraphOwnershipLoadResult` with the one matching record or positive initialized-ledger `VerifiedAbsent`; ambiguous/foreign/corrupt metadata returns `Unavailable`. It is a lookup over the fixed current ledger, not a new descriptor/schema. A found record is then reloaded through existing `LoadAsync(record)` before use. Create an `Absent` draft with a new `OwnershipId` only after that positive absence and absent fixed entry. Missing/corrupt metadata, stale approval, foreign Ready, substituted root/entry, ambiguous owner or unprotected placement is `Unavailable`, not a new draft. A current `Ready` record after restart still requires prior-generation resource settlement before any `VerifiedOwned` view or disposition. No new database table, ledger format, state name, source copy or standalone workspace manager is added. Existing `GetOwnershipViewAsync` remains the one reader/JDT classification producer.

The registry is one process-scoped instance for the exact approval/root/owner key. Production `PreparedToolWorkspaceIssuer` construction additionally takes this internal registry without changing its public `IssueAsync` or Worker surface: issue registers a lease before return, and lease disposal releases it only after its contained operations settle. The JDT factory/context registers its process/session until actual protocol and process disposal. Story 18.3 registers one borrow before launching or connecting its native CodeGraph child; its probe inspects the owned child tree/job, MCP connection, in-flight calls and held source/state handles. `BeginBorrowAsync` returns a ticket only while admission is open and exact source/tool/root/owner authority is current. A failed start, cancellation or disposal with unknown descendants/handles keeps the ticket `Unknown`; a parent PID exit, MCP `DisposeAsync` call or caller-supplied `true` cannot mark it settled. The native implementation and its handle checks remain Story 18.3-owned.

Before `DisposeOwnedStateAsync`, trusted composition calls `CloseForDispositionAsync` for the exact current binding. It latches closure against concurrent issuance and borrow, checks every lease registration and live settlement probe, and returns `Settled` only after all are verified closed. The existing `IPreparedCodeGraphWorkspaceOwnershipVerifier.VerifyDispositionReadyAsync` checks this latched status plus fresh exact owner/root/current-source/tool authority before each bounded deletion chunk; it cannot derive readiness from the coordinator callback ending. Closure remains latched through durable `Removed` readback or quarantine. On restart, process-local emptiness is `Unknown` for a persisted record: a new trusted admission must independently reconcile the prior generation's child/handle ownership before use or cleanup. Until the native owner can supply that proof, no Ready view, new borrow or disposition proceeds. `Creating`/`Disposing`/uncertain terminal state remains unavailable under the existing store rules. No new operator UI or automatic by-name cleanup is implied.

The default Web composition stays file-only. Only after current operator admission/source-tool providers, selected-host root and metadata protection, exact binding, the registry, coordinator and protected store are qualified may a trusted production registration supply a bound JDT factory or enable `CodeGraphPreparation`. Real JDT additionally requires its distinct restricted launcher/profile and first-running-host-`PATH` qualification; this binding does not grant a native launch. Story 18.3 consumes the same typed binding and registry after a separate verified shared checkpoint and its own approval, then supplies real borrower/child/handle settlement and reader/native readiness evidence. CodeGraph-enabled application composition must remain closed until 18.3 qualifies ordinary list/read/search exclusion of the owned physical entry, direct read access, budgets/continuations and native readiness; the 18.2 binding/hold does not repair ordinary file-tool traversal. If the actual operator admission or host protection cannot meet this contract without a new authority, owner, schema or root, return for a fresh scoped architecture decision.

### Trusted Web application composition

The application-facing callable is a small `Vulgata.Web` service, separate from the Worker functions and the Infrastructure factory. It uses existing 18.1 and 18.2 types without changing their contracts. The intended public shape is:

```csharp
public sealed class PreparedJavaWorkerToolComposer
{
    public PreparedJavaWorkerToolComposer(
        PreparedToolWorkspaceIssuer? workspaceIssuer,
        PreparedJavaNavigationContextFactory? navigationFactory);
    public Task<PreparedJavaWorkerToolComposition> ComposeAsync(
        IPreparedSourceReadContext alreadyIssuedSourceContext,
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        PreparedToolWorkspaceGrantRequest applicationSelectedJdtGrant,
        CancellationToken cancellationToken = default);
}
public sealed class PreparedJavaWorkerToolComposition : IAsyncDisposable
{
    public PreparedJavaNavigationOpenStatus NavigationStatus { get; }
    public IReadOnlyList<AIFunction> SourceFunctions { get; }
    public IReadOnlyList<AIFunction> NavigationFunctions { get; }
    public IReadOnlyList<AIFunction> WorkerFunctions { get; }
    public ValueTask DisposeAsync();
}
```

`Program.cs` registers the composer as a scoped service with an explicit DI factory equivalent to `services.AddScoped<PreparedJavaWorkerToolComposer>(sp => new(sp.GetService<PreparedToolWorkspaceIssuer>(), sp.GetService<PreparedJavaNavigationContextFactory>()))`. `GetService` permits absent optional navigation dependencies without constructing or resolving a runtime. The default application registers neither a production-capable JDT runtime nor an issuer/factory with missing current-authority and tool-purpose verifiers. It also cannot register an enabled navigation factory without the exact application-held workspace binding, current ownership coordinator and qualified durable store; unresolved optional dependencies yield a file-only `ConfigurationUnavailable` composition. A controlled application test registers real issuer/factory instances backed by controlled source/tool authorizers, the exact owned fixture binding and coordinator/store, a fixed `PreparedJavaNavigationProfile` and fake `IPreparedJdtLsRuntime`, then resolves and calls this same Web service. Later production registration requires the T022/T023 accepted trusted PATH/process boundary and fixed profile as well as that ownership binding; a configured command name or fake view alone does not enable it. The existing 18.1 `PreparedSourceFunctions` can also be used independently of this service.

The SelfContained T012 test reaches that real registration by creating a test `WebApplicationBuilder`, supplying only a disposable placeholder `ConnectionStrings:DefaultConnection` value required by `VulgataWebComposition.ConfigureServices(builder)`, calling that public composition method, and then adding controlled issuer/factory service registrations. It builds a service provider and resolves the scoped composer without starting the Web host, database migration, hosted services or a JDT process. The test does not replace the composer registration. This is a direct check of the application composition code in `Program.cs`; a `WebApplicationFactory<Program>` native host run is unnecessary for this controlled service seam and has separate database/startup costs.

Only trusted application code supplies the already-issued read context, its application-held `PreparedSourceGrantRequest` and the `PreparedToolWorkspaceGrantRequest`; the Worker receives only `WorkerFunctions` and can choose only the existing relative tool arguments. The application must pass the context it actually issued from that source grant. The composer requires the two grants' `PreparedSourceApproval` records to be exactly equal, their `PhysicalRootPath` strings to be exactly equal, the JDT grant purpose to be `JdtNavigation`, and the context's Repository ID to equal that approval's Repository ID. This rejects even a same-Repository grant with changed approval identity, root-binding identity or physical root before issuance. Since 18.1 intentionally exposes no full approval/root from `IPreparedSourceReadContext`, the trusted caller's context-from-source-grant pairing is a precondition, not a claim the Repository-ID check can prove. The T012 fixture issues the context from its supplied source grant and then exercises matching and deliberately mismatched JDT grants. A null read context is a caller error; a null/malformed grant or failed pairing returns three source functions, zero navigation functions and `Denied`, with no JDT issuance or runtime call. `SourceFunctions` are the existing three `PreparedSourceFunctions.Create()` functions. `NavigationFunctions` are exactly the two `PreparedJavaNavigationFunctions.Create()` functions only on `Opened`; `WorkerFunctions` is their stable source-first concatenation, with no extra function or model-visible trusted status. All other statuses have empty navigation functions. The composer maps issuer `Denied`, `AuthorityUnavailable`, `RootUnavailable`, `Cancelled` and `DeadlineExceeded` to the same navigation open statuses; `UnsupportedPlatform` or missing trusted registration maps to `ConfigurationUnavailable`. It preserves a sanitized factory open status, including `CleanupUnconfirmed`, internally without exposing a root or partial protocol payload to Worker tools.

The composition borrows the 18.1 read context for its entire lifetime; the trusted caller disposes that context separately after all file calls. On issue/open failure, or cancellation before handoff, the composer disposes any issued navigation lease before returning a file-only result. On success, the composition owns the navigation context and then the lease; after all Worker calls settle, `DisposeAsync` disposes context first and lease in a `finally`, even if context disposal fails. It never disposes the borrowed read context. Per-call cancellation still passes through the existing functions/context, and composing with an already-cancelled token starts no JDT work and returns file-only `Cancelled`. A missing navigation configuration, denied authority, limited result or null-context failure cannot remove the separately authorized 18.1 functions. Controlled tests can assert this callable from `Program.cs` fails RED before implementation, without manually assembling lower components or starting Java.

## Process and source protection

Trusted Infrastructure uses the running host `PATH` as the sole source of JDT-LS/Java command candidates. It walks entries in order for exact supported names (`jdtls.exe` or `jdtls.bat`; `java.exe`) rather than launching a bare name, consulting `PATHEXT`, `SearchPath`, `Get-Command` selection or leaving Windows to search application/current/system directories. Empty, relative and current-directory entries fail resolution instead of authorizing a candidate; two supported JDT names in the same first matching directory are ambiguous and also fail. If the first matching candidate is untrusted, fail closed without trying a later entry. No config-file executable override, user-profile fallback or `.env`/`JDT_LS_BIN` discovery is permitted. If no trusted command is visible and qualification is blocked, pause and report; an unrestarted process may have a stale `PATH`, but a guessed fallback is not authorized.

For each first match, resolve its physical directory and ancestors without following an untrusted reparse redirect; reject a candidate within the prepared root, disposable tool state, current directory or another host-untrusted PATH entry. Require a regular non-reparse file with the exact expected extension in a host-approved executable directory that Worker/tool identities cannot write or replace. Pin its canonical absolute path, file identity and content digest for the session; keep a read handle that prevents replacement where supported and recheck identity immediately before launch. A changed file, ambiguous path, unsupported type or unverifiable parent is an unsafe candidate and starts no process. The application passes the validated absolute target, never a bare command, to process creation. These are trusted host-location checks, not a new model-selectable executable setting.

For `jdtls.bat`, the fixed adapter may use only a validated absolute system interpreter with a fixed argument shape; it must not resolve `cmd.exe` from an untrusted `COMSPEC` or current directory. The interpreter, script, Java child and any permitted descendants run under the same approved least-privilege process boundary with a constrained child `PATH` containing only approved resolved tool/system inputs. An unexpected child or expansion outside that policy fails qualification; no Worker shell/command tool is exposed. JDT-LS receives the whole prepared Repository as `rootUri`, with separate writable `-data` and Equinox configuration. Its effective settings disable automatic Maven/Gradle import, builds and dependency restore. A trusted root lease and URI filtering do not restrict process reads/writes: the declared host profile must enforce least privilege so prepared source is read-only and unrelated host files are inaccessible except declared Java/JDT runtime inputs. Unsafe launch fails closed pending design correction. No source mirror/copy or Git identity is granted.

## Representative cases

- Valid: the application issues a current batch/Repository/subject/root binding for `JdtNavigation`; a Worker asks for a definition at a relative Java path and receives only contained relative locations.
- Invalid: a Worker asks for `C:\\other\\Secret.java`, `../other/Secret.java`, a symlink escape, another Repository or a different executable. Return a sanitized denial, never an outside-root location.
- Raw protocol denial: `file:///?/C:/outside/Secret.java`, literal `\\?\\C:\\outside\\Secret.java`, UNC authority and percent-encoded traversal are rejected before `System.Uri` normalization. A constructed `Uri` is not the test input for the device-path case.
- Owned CodeGraph state: under a current `CodeGraphPreparation` lease and a positive dedicated-workspace owner binding, the trusted coordinator exclusively creates one `.codegraph` entry, uses contained descendants and removes only that recorded entry at workspace disposition. A preexisting or substituted entry, wrong purpose or authority loss yields no unauthorized **application-controlled** root/sibling write. The pinned native child inherits host rights; Story 18.3's observed-footprint/readiness checks and residual-risk report are separate.
- Authority loss: after JDT-LS buffers two locations, approval is revoked or a verifier becomes unavailable before disclosure. Every concurrent request releases zero locations/counts; the lease remains closed until new issuance.
- Limited: missing classpath yields no observed location and an explicit limitation; Story 18.1's three read functions stay separately callable, and CodeGraph remains separately required.
