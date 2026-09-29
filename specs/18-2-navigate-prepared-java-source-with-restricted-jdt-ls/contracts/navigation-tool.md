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

## Process and source protection

October application config files explicitly select the Java executable and JDT-LS installation/distribution; missing config fails closed. Existing `AnalysisRuntime:JavaHome`/`JdtLsInstallationRoot` and Java-plus-launcher-jar path are mechanism candidates, not a mandate to use the historical `bin/jdtls.bat` or user-profile fallback. Only real JDT-LS tests may load `JDT_LS_BIN` from their local `.env`; ordinary composition does not discover arbitrary `.env` files. JDT-LS receives the whole prepared Repository as `rootUri`, with separate writable `-data` and Equinox configuration. Its effective settings disable automatic Maven/Gradle import, builds and dependency restore. A trusted root lease and URI filtering do not restrict process reads/writes: the declared host profile must enforce least privilege so prepared source is read-only and unrelated host files are inaccessible apart from declared Java/JDT runtime inputs. Unsafe launch fails closed pending design correction. No source mirror/copy, Git identity or shell/command tool is granted.

## Representative cases

- Valid: the application issues a current batch/Repository/subject/root binding for `JdtNavigation`; a Worker asks for a definition at a relative Java path and receives only contained relative locations.
- Invalid: a Worker asks for `C:\\other\\Secret.java`, `../other/Secret.java`, a symlink escape, another Repository or a different executable. Return a sanitized denial, never an outside-root location.
- Authority loss: after JDT-LS buffers two locations, approval is revoked or a verifier becomes unavailable before disclosure. Every concurrent request releases zero locations/counts; the lease remains closed until new issuance.
- Limited: missing classpath yields no observed location and an explicit limitation; Story 18.1's three read functions stay separately callable, and CodeGraph remains separately required.
