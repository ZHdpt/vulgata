# Story 18.2 Navigation and Trusted Workspace Contracts

This is a plan contract for the Story 18.2-owned shared physical-workspace seam and its JDT-LS consumer. It preserves the integrated Story 18.1 read contract at `specs/016-contained-source-tools/contracts/prepared-source-tools.md`. Story 18.3 consumes only the JDT-independent shared portion after a separately authorized, identifiable verified checkpoint; this document is neither approval nor an implemented API.

## Shared trusted physical-workspace seam

The trusted application supplies the existing exact `PreparedSourceApproval` tuple (batch, Repository, subject, `WorkerSourceRead`, approval identity, root-binding identity and operator label), physical prepared root, selected tool purpose and lease deadline. `WorkerSourceRead` authorizes the read context only; a separate current tool-purpose authorization is mandatory for `JdtNavigation` or `CodeGraphPreparation`. A missing verifier/authorizer has no default allow implementation. The root is retained as a no-follow handle with observed identity; it is not exposed through `IPreparedSourceReadContext` or Agent arguments.

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
    public ValueTask DisposeAsync();
    // Root handle and physical path are internal to Infrastructure adapters.
}
```

The status families distinguish issued/current, denied, authority unavailable, root unavailable, unsupported platform, cancelled and deadline exceeded without exposing trusted input. `Issued` has exactly one lease; every other status has none. Null or malformed construction/request inputs fail closed. The trusted caller owns async disposal after adapter calls settle. A `JdtNavigation` lease may cover one bounded JDT-LS session; Story 18.3 obtains a distinct `CodeGraphPreparation` lease for its own operation and cannot reuse JDT-LS permission or session lifetime. Cancellation/deadline close the affected lease; a new approval or restored verifier never reopens it.

Issuer and lease check the exact existing `PreparedSourceApproval` through `IPreparedSourceCurrentAuthorityVerifier` and the selected tool purpose through `IPreparedToolUseAuthorizer` at issuance, before each source/protocol operation, between bounded work chunks and immediately before disclosure. Revoked/changed approval, denied purpose or unavailable verifier/authorizer latches the lease closed, cancels owned work and discards buffered results. No cached allow is sufficient. The same grant/root identity is never silently rebound. Story 18.3 may use the lease for its native CodeGraph preparation without referencing JDT-LS classes, but must still prove its own subprocess/tool containment. A path lease itself grants no claim that any subprocess is contained.

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
    Uri Uri, int StartLine, int StartCharacter, int EndLine, int EndCharacter);
```

The application constructs `PreparedJavaNavigationProfile` from trusted bounds and a disposable state root outside the prepared source; it contains no executable selector. The caller first obtains a `PreparedToolWorkspaceLease` with `JdtNavigation` purpose. Factory `OpenAsync` checks that purpose, lease lifetime/current authority, root identity and profile/state separation and reserves the one-session-per-Repository slot before calling the injected runtime. `Opened` has exactly one context; every other status has null context and no model-visible trusted details. Null programmer inputs fail without launch.

Ownership has three distinct phases. **Before runtime return**, `IPreparedJdtLsRuntime.OpenAsync` owns every process, protocol and disposable-state resource it starts. If opening fails or is cancelled before returning `IPreparedJdtLsSession`, it performs bounded cleanup, then throws `PreparedJdtLsOpenFailureException` with a sanitized status and whether cleanup was confirmed; the factory cannot dispose handles it never received. A confirmed failure lets the factory release its slot and return that sanitized status with null context. An unconfirmed failure (or unexpected exception without cleanup attestation) maps to `CleanupUnconfirmed` with null context, keeps the slot unavailable and forbids retry until owned-resource resolution; it cannot be reported as safe `Cancelled`. A second open for the same Repository while quarantined returns `CleanupUnconfirmed` without calling the runtime. If this trusted status must be surfaced through a Worker function, map it to existing `PreparedJavaNavigationOutcome.ConfigurationUnavailable` with empty locations, counts and details. The exception never carries raw paths/source/approval or protocol details into the Worker result. **After session return but before context handoff**, the factory owns the returned session. If cancellation, a current-lease/profile check or context construction then fails, it disposes that session before mapping a sanitized null-context status and releasing its session slot. **After successful context handoff**, the context owns and disposes the returned session and releases the slot after Worker calls settle. The context borrows the separately issued lease; the trusted caller disposes the lease after the context, or after a failed open. The factory never disposes that caller-owned lease. A failed/disposed context or partial runtime session cannot be reused.

The injected `IPreparedJdtLsRuntime` is selected only by trusted Infrastructure composition. A controlled fake may report raw protocol replies and record its own pre-return cleanup or a returned session's later disposal for T010/T011/T013/T018 without starting Java or JDT-LS. The context validates and maps raw URIs to the granted relative root, performs current-authority/pre-disclosure checks and sanitizes outcomes; the fake cannot turn raw locations into authorized output by itself. Fake runtime behavior cannot prove real `rootUri`, `-data`/configuration placement or fixed JDT settings; those require observable production-runtime checks and T022–T024 qualification. Production composition must remain unregistered/fail closed for a real runtime until accepted T022/T023 trusted PATH and process-profile evidence. The eventual real runtime reuses assessed stdio/StreamJsonRpc/process mechanics behind this interface, never the legacy `AnalysisScope` factory or revision lease. No caller passes a model-selected root, command, adapter or physical workspace through `OpenAsync`.

## Process and source protection

Trusted Infrastructure uses the running host `PATH` as the sole source of JDT-LS/Java command candidates. It walks entries in order for exact supported names (`jdtls.exe` or `jdtls.bat`; `java.exe`) rather than launching a bare name, consulting `PATHEXT`, `SearchPath`, `Get-Command` selection or leaving Windows to search application/current/system directories. Empty, relative and current-directory entries fail resolution instead of authorizing a candidate; two supported JDT names in the same first matching directory are ambiguous and also fail. If the first matching candidate is untrusted, fail closed without trying a later entry. No config-file executable override, user-profile fallback or `.env`/`JDT_LS_BIN` discovery is permitted. If no trusted command is visible and qualification is blocked, pause and report; an unrestarted process may have a stale `PATH`, but a guessed fallback is not authorized.

For each first match, resolve its physical directory and ancestors without following an untrusted reparse redirect; reject a candidate within the prepared root, disposable tool state, current directory or another host-untrusted PATH entry. Require a regular non-reparse file with the exact expected extension in a host-approved executable directory that Worker/tool identities cannot write or replace. Pin its canonical absolute path, file identity and content digest for the session; keep a read handle that prevents replacement where supported and recheck identity immediately before launch. A changed file, ambiguous path, unsupported type or unverifiable parent is an unsafe candidate and starts no process. The application passes the validated absolute target, never a bare command, to process creation. These are trusted host-location checks, not a new model-selectable executable setting.

For `jdtls.bat`, the fixed adapter may use only a validated absolute system interpreter with a fixed argument shape; it must not resolve `cmd.exe` from an untrusted `COMSPEC` or current directory. The interpreter, script, Java child and any permitted descendants run under the same approved least-privilege process boundary with a constrained child `PATH` containing only approved resolved tool/system inputs. An unexpected child or expansion outside that policy fails qualification; no Worker shell/command tool is exposed. JDT-LS receives the whole prepared Repository as `rootUri`, with separate writable `-data` and Equinox configuration. Its effective settings disable automatic Maven/Gradle import, builds and dependency restore. A trusted root lease and URI filtering do not restrict process reads/writes: the declared host profile must enforce least privilege so prepared source is read-only and unrelated host files are inaccessible except declared Java/JDT runtime inputs. Unsafe launch fails closed pending design correction. No source mirror/copy or Git identity is granted.

## Representative cases

- Valid: the application issues a current batch/Repository/subject/root binding for `JdtNavigation`; a Worker asks for a definition at a relative Java path and receives only contained relative locations.
- Invalid: a Worker asks for `C:\\other\\Secret.java`, `../other/Secret.java`, a symlink escape, another Repository or a different executable. Return a sanitized denial, never an outside-root location.
- Authority loss: after JDT-LS buffers two locations, approval is revoked or a verifier becomes unavailable before disclosure. Every concurrent request releases zero locations/counts; the lease remains closed until new issuance.
- Limited: missing classpath yields no observed location and an explicit limitation; Story 18.1's three read functions stay separately callable, and CodeGraph remains separately required.
