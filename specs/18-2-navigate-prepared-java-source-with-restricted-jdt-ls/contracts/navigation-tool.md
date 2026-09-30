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

Issuer and lease check the exact existing `PreparedSourceApproval` through `IPreparedSourceCurrentAuthorityVerifier` and the selected tool purpose through `IPreparedToolUseAuthorizer` at issuance, before each source/protocol operation, between bounded work chunks and immediately before disclosure. Revoked/changed approval, denied purpose or unavailable verifier/authorizer latches the lease closed, cancels owned work and discards buffered results. No cached allow is sufficient. The same grant/root identity is never silently rebound. Story 18.3 may use the lease for its native CodeGraph preparation without referencing JDT-LS classes, but must still prove its own subprocess/tool containment. A path lease itself grants no claim that any subprocess is contained.

`UseContainedRootAsync` is the existing trusted physical-root read/binding operation; it is `internal` in Infrastructure and is never part of `IPreparedSourceReadContext`, a Worker function, a serialized result or a public path property. It rejects an unexpected tool purpose, non-open lease, unavailable/mismatched exact current approval, deadline/cancellation or changed canonical root object identity before invoking the callback. It serializes the short callback with lease disposal under one gate, reusing an internal check routine without recursively entering public `CheckCurrentAsync`, and rechecks current authority and pinned root object identity afterward. The callback receives a token linked to caller cancellation and lease deadline and only a short-lived `PreparedToolRootScope`, invalidated on completion/closure; the callback returns no physical path or arbitrary value. On failure or an exception, the operation returns a sanitized non-current status, and consumers discard derived bindings/results. This callback is limited to bounded root binding, separate JDT state-root comparison or URI mapping; no unbounded JDT protocol/navigation wait or Worker disclosure occurs inside it. A launch binding copied by trusted Infrastructure from the callback requires another live identity/authority check immediately before launch. The same stable object check applies to JDT and CodeGraph leases so legitimate `.codegraph` parent metadata changes do not close a JDT session; per-file evidence and a restricted child process, not root-directory mtime, establish source no-write claims.

`GetPhysicalRootPathForTrustedToolBinding` is callable only inside that internal callback by trusted adapters. It lets the JDT adapter create `rootUri` and the CodeGraph adapter bind its source input, while the lease retains the private root handle/identity and no path reaches Agent code. `IsSeparateDisposableStateRoot` resolves the configured JDT tool-state directory and rejects overlap in either direction or reparse redirection into the prepared root; it remains the JDT rule and is not weakened for CodeGraph. `TryMapContainedFileUri` takes the JSON-decoded protocol string before `Uri.TryCreate` or other URI/path normalization. First inspect exact raw syntax: require the canonical absolute local `file:` form for this host; reject backslashes, device/extended-length prefixes, UNC/authority hosts, query/fragment, empty/dot segments, malformed or encoded path syntax that can change interpretation, and non-file schemes. Only then derive a path and verify it through held-root handle-relative no-follow traversal. Reject outside roots, symlink/reparse escapes, nonexistent/unreadable targets and changed root identity. It yields only `PreparedSourceLocation` on success, never an outside physical path or raw URI. A `System.Uri` parameter cannot faithfully test raw Windows `\\?\\` or `\\.\\` device spellings because construction may reject or normalize them first. The production protocol parser must retain `JsonElement.GetString()` as the raw location string through this check; a test supplies literal raw protocol strings at that same seam. For every operation, current authority is checked again immediately before any Worker response; authority loss discards already mapped locations.

### Positive owned CodeGraph state binding

The existing root callback is a read/binding operation and `IsSeparateDisposableStateRoot` continues to reject in-root JDT state. A separate internal CodeGraph-only operation is proposed in `Vulgata.Infrastructure.Source`; it accepts neither an arbitrary state-entry name nor a caller-supplied directory. Trusted application composition must establish a non-model `PreparedCodeGraphWorkspaceOwnership` for an explicitly dedicated managed prepared workspace; no such workspace-manager service is implemented merely by this contract. The positive record binds the exact `PreparedSourceApproval` (including batch, Repository, subject, approval and root-binding identities), canonical physical root and stable observed root object identity (volume/file index, not mutable directory last-write time), workspace owner identity, fixed `.codegraph` name and, after exclusive creation, the created entry's physical identity. It is retained outside `.codegraph` across individual tool leases until disposition and must be recoverable and independently verified after restart. An absent/foreign/unverifiable record or a preexisting `.codegraph` entry cannot be adopted. A validated internal view of this positive binding is the only way Story 18.3's ordinary reader may classify this entry as application-owned tool state; the name alone is insufficient.

The intended internal callable shape is:

```csharp
internal sealed record PreparedToolDirectoryObjectIdentity(
    uint VolumeSerialNumber, uint FileIndexHigh, uint FileIndexLow);
internal sealed record PreparedCodeGraphWorkspaceOwnership(
    PreparedSourceApproval Approval,
    string PhysicalRootPath,
    PreparedToolDirectoryObjectIdentity RootObjectIdentity,
    string WorkspaceOwnerIdentity,
    PreparedToolDirectoryObjectIdentity? StateEntryObjectIdentity);

internal sealed class PreparedCodeGraphStateCoordinator
{
    internal PreparedCodeGraphStateCoordinator(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        IPreparedCodeGraphWorkspaceOwnershipVerifier workspaceOwnershipVerifier);
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
    // Trusted workspace-manager verification, never a Worker argument.
    // Checks the exact dedicated workspace and owner record for each use/disposition.
    ValueTask<bool> VerifyCurrentAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken cancellationToken = default);
    // Trusted manager confirms new grants stopped and CodeGraph child/handles settled.
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

The ownership record is issued and verified by trusted composition, never constructed from a Worker/tool result. `StateEntryObjectIdentity == null` means no entry has been created and is valid only before the exclusive create; an owned entry requires the recorded non-null object identity. The only accepted state-entry name is the coordinator's fixed `.codegraph` constant, not a record or call argument. Object identities intentionally omit mutable directory last-write time. The owner verifier must confirm the record's dedicated-workspace owner, exact grant/root and current ownership state; field equality alone is not authorization. A reader may use only a current verified non-null owned-entry record under the same approval/root binding; an unowned `.codegraph` name remains ordinary source subject to 18.3's reader policy.

`UseOwnedStateAsync` checks a current `CodeGraphPreparation` lease, exact approval/tool authority, owner record and pinned root object identity before and after its bounded callback. It exclusively creates one absent `.codegraph` root entry using the held prepared-root handle, records its physical identity, and gives the trusted adapter only a short-lived state scope. Creation changes the prepared-root directory last-write time; the current `PreparedSourceTraversal.DirectoryIdentity` includes that mutable field, so the CodeGraph state operation must compare stable root object identity while still rejecting root replacement. It must not treat expected cache-entry metadata changes as proof of source mutation. JDT's separate-state validation is unchanged; Story 18.3 owns the ordinary reader's corresponding in-place state handling. Later leases verify the recorded identity and may use only that owned subtree. Any direct application descendant create/open/modify operation is handle-relative with no-follow traversal and verifies ownership/current authority before mutation; symlinks/reparse substitutes, hardlink aliases to source, traversal, siblings and another state name are denied. A copied trusted child binding is revalidated immediately before launch. The callback does not encompass an unbounded native MCP session; the adapter checks live authority during use and before disclosure. A post-check failure cannot report success and closes the lease. If mutation may have occurred at authority loss, quarantine the workspace; no ordinary retry adopts or deletes the entry.

`DisposeOwnedStateAsync` is invoked by trusted application composition only at workspace disposition after stopping new grants and settling CodeGraph children/handles. `dispositionApproval` is the currently selected, application-held approval, never a Worker argument; it must exactly equal the approval in the positive owner record. The coordinator's injected *existing* `IPreparedSourceCurrentAuthorityVerifier` must return `Current` with `CurrentApproval == dispositionApproval`, and its injected *existing* `IPreparedToolUseAuthorizer` must return `Current` for that approval and the hard-coded `CodeGraphPreparation` purpose. The ownership verifier's disposition-ready check must confirm stopped grants and settled child/handles from the trusted workspace/process owners; a caller assertion alone is insufficient. Before each bounded cleanup chunk, the coordinator checks these fresh results, `workspaceOwnershipVerifier.VerifyCurrentAsync`, `VerifyDispositionReadyAsync`, the dedicated workspace/owner record, and reopened held no-follow root plus recorded entry identity. After removal it repeats the fresh authority/owner/readiness and root-object checks, and verifies that the recorded entry is absent; there is no removed entry identity to compare. A substituted entry is never deleted by name. Neither the record nor a previously granted or revoked lease supplies current source/tool authority. A denied, changed, unavailable, cancelled or timed-out check prevents further deletion and cannot report success. If cleanup has begun, if a child/handle is not demonstrably settled, or if the post-check fails, the workspace is quarantined and unavailable for reuse; only explicit owner resolution can handle partial state, never a by-name retry. The coordinator removes only the recorded `.codegraph` entry and descendants through held-root-relative no-follow operations, never source or siblings. A resumed workspace with lost/unverifiable record is quarantined rather than adopted by name. This contract does not prescribe a new database schema or general-purpose workspace-write API.

For Story 18.3's returned source excerpts, the integrated `IPreparedSourceReadContext.ReadTextRangeAsync` supplies bounded held-root-relative no-follow source reads, but its current root metadata and traversal behavior are not yet qualified for in-place cache writes. After Story 18.3's reader correction, trusted composition may use it only under the same exact approval/root binding as the current `CodeGraphPreparation` lease, after mapping the native location to a contained relative path; compare the bounded bytes and treat `Limited`, `ChangedInput`, denied or unavailable reads as no verified excerpt. Recheck both source-read and CodeGraph-purpose authority before disclosure. This reuses Story 18.1's public read contract; Story 18.3 owns the reader integration and continuation repair, and 18.2 adds no second source-reader/filter contract. The positive owner binding needed to exclude cache from ordinary reading is an internal verified producer output, not a lexical `.codegraph` rule or a Worker-visible handle.

These controlled operations prove application-side scope and ownership only. A native CodeGraph child is not constrained by an internal C# callback or state path; Story 18.3 must qualify its filesystem and network process boundary, native MCP use, source no-write behavior and reader/continuation repair before in-place integration is safe. Story 18.2 owns this shared seam and focused tests. Story 18.3 owns adapter/index/session code and `PreparedSourceReadContext.cs`/agreed traversal repair, with 18.8/18.12 downstream. The changed seam requires new independent plan/definition reviews and applicable bound approvals before implementation or 18.3 consumption.

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

The application constructs `PreparedJavaNavigationProfile` from trusted bounds and a disposable state root outside the prepared source; it contains no executable selector. The caller first obtains a `PreparedToolWorkspaceLease` with `JdtNavigation` purpose. Factory `OpenAsync` checks that purpose, lease lifetime/current authority, root identity and profile/state separation and reserves the one-session-per-Repository slot before calling the injected runtime. `Opened` has exactly one context; every other status has null context and no model-visible trusted details. Null programmer inputs fail without launch.

Ownership has three distinct phases. **Before runtime return**, `IPreparedJdtLsRuntime.OpenAsync` owns every process, protocol and disposable-state resource it starts. If opening fails or is cancelled before returning `IPreparedJdtLsSession`, it performs bounded cleanup, then throws `PreparedJdtLsOpenFailureException` with a sanitized status and whether cleanup was confirmed; the factory cannot dispose handles it never received. A confirmed failure lets the factory release its slot and return that sanitized status with null context. An unconfirmed failure (or unexpected exception without cleanup attestation) maps to `CleanupUnconfirmed` with null context, keeps the slot unavailable and forbids retry until owned-resource resolution; it cannot be reported as safe `Cancelled`. A second open for the same Repository while quarantined returns `CleanupUnconfirmed` without calling the runtime. If this trusted status must be surfaced through a Worker function, map it to existing `PreparedJavaNavigationOutcome.ConfigurationUnavailable` with empty locations, counts and details. The exception never carries raw paths/source/approval or protocol details into the Worker result. **After session return but before context handoff**, the factory owns the returned session. If cancellation, a current-lease/profile check or context construction then fails, it disposes that session before mapping a sanitized null-context status and releasing its session slot. **After successful context handoff**, the context owns and disposes the returned session and releases the slot after Worker calls settle. The context borrows the separately issued lease; the trusted caller disposes the lease after the context, or after a failed open. The factory never disposes that caller-owned lease. A failed/disposed context or partial runtime session cannot be reused.

The injected `IPreparedJdtLsRuntime` is selected only by trusted Infrastructure composition. A controlled fake may report raw protocol replies and record its own pre-return cleanup or a returned session's later disposal for T010/T011/T013/T018 without starting Java or JDT-LS. Its `RawProtocolUri` is the JSON-decoded location field before `System.Uri` construction, so actual Windows device spellings can reach the denial seam and be tested. The context validates and maps those raw strings to the granted relative root, performs current-authority/pre-disclosure checks and sanitizes outcomes; the fake cannot turn raw locations into authorized output by itself. Fake runtime behavior cannot prove real `rootUri`, `-data`/configuration placement or fixed JDT settings; those require observable production-runtime checks and T022–T024 qualification. Production composition must remain unregistered/fail closed for a real runtime until accepted T022/T023 trusted PATH and process-profile evidence. The eventual real runtime reuses assessed stdio/StreamJsonRpc/process mechanics behind this interface, never the legacy `AnalysisScope` factory or revision lease. No caller passes a model-selected root, command, adapter or physical workspace through `OpenAsync`.

## Process and source protection

Trusted Infrastructure uses the running host `PATH` as the sole source of JDT-LS/Java command candidates. It walks entries in order for exact supported names (`jdtls.exe` or `jdtls.bat`; `java.exe`) rather than launching a bare name, consulting `PATHEXT`, `SearchPath`, `Get-Command` selection or leaving Windows to search application/current/system directories. Empty, relative and current-directory entries fail resolution instead of authorizing a candidate; two supported JDT names in the same first matching directory are ambiguous and also fail. If the first matching candidate is untrusted, fail closed without trying a later entry. No config-file executable override, user-profile fallback or `.env`/`JDT_LS_BIN` discovery is permitted. If no trusted command is visible and qualification is blocked, pause and report; an unrestarted process may have a stale `PATH`, but a guessed fallback is not authorized.

For each first match, resolve its physical directory and ancestors without following an untrusted reparse redirect; reject a candidate within the prepared root, disposable tool state, current directory or another host-untrusted PATH entry. Require a regular non-reparse file with the exact expected extension in a host-approved executable directory that Worker/tool identities cannot write or replace. Pin its canonical absolute path, file identity and content digest for the session; keep a read handle that prevents replacement where supported and recheck identity immediately before launch. A changed file, ambiguous path, unsupported type or unverifiable parent is an unsafe candidate and starts no process. The application passes the validated absolute target, never a bare command, to process creation. These are trusted host-location checks, not a new model-selectable executable setting.

For `jdtls.bat`, the fixed adapter may use only a validated absolute system interpreter with a fixed argument shape; it must not resolve `cmd.exe` from an untrusted `COMSPEC` or current directory. The interpreter, script, Java child and any permitted descendants run under the same approved least-privilege process boundary with a constrained child `PATH` containing only approved resolved tool/system inputs. An unexpected child or expansion outside that policy fails qualification; no Worker shell/command tool is exposed. JDT-LS receives the whole prepared Repository as `rootUri`, with separate writable `-data` and Equinox configuration. Its effective settings disable automatic Maven/Gradle import, builds and dependency restore. A trusted root lease and URI filtering do not restrict process reads/writes: the declared host profile must enforce least privilege so prepared source is read-only and unrelated host files are inaccessible except declared Java/JDT runtime inputs. Unsafe launch fails closed pending design correction. No source mirror/copy or Git identity is granted.

## Representative cases

- Valid: the application issues a current batch/Repository/subject/root binding for `JdtNavigation`; a Worker asks for a definition at a relative Java path and receives only contained relative locations.
- Invalid: a Worker asks for `C:\\other\\Secret.java`, `../other/Secret.java`, a symlink escape, another Repository or a different executable. Return a sanitized denial, never an outside-root location.
- Raw protocol denial: `file:///?/C:/outside/Secret.java`, literal `\\?\\C:\\outside\\Secret.java`, UNC authority and percent-encoded traversal are rejected before `System.Uri` normalization. A constructed `Uri` is not the test input for the device-path case.
- Owned CodeGraph state: under a current `CodeGraphPreparation` lease and a positive dedicated-workspace owner binding, the trusted coordinator exclusively creates one `.codegraph` entry, uses contained descendants and removes only that recorded entry at workspace disposition. A preexisting or substituted entry, wrong purpose or authority loss yields no root/sibling write; native child confinement remains a separate 18.3 qualification.
- Authority loss: after JDT-LS buffers two locations, approval is revoked or a verifier becomes unavailable before disclosure. Every concurrent request releases zero locations/counts; the lease remains closed until new issuance.
- Limited: missing classpath yields no observed location and an explicit limitation; Story 18.1's three read functions stay separately callable, and CodeGraph remains separately required.
