using System.Runtime.CompilerServices;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;

[assembly: InternalsVisibleTo("Vulgata.Tests")]

namespace Vulgata.OctoberGenerator;

internal sealed record OctoberToolRuntimeDependencies(
    Func<TimeSpan, CancellationToken, Task<PreparedCodeGraphConnection>> PrepareCodeGraph,
    Func<PreparedCodeGraphConnection?, TimeSpan, CancellationToken, Task<PreparedSourceIssueResult>> IssueSource,
    Func<IPreparedSourceReadContext, string, CancellationToken, Task<string?>> ObserveIdentity,
    Func<TimeSpan, CancellationToken, Task<IPreparedJavaNavigationContext>> OpenDedicatedJava,
    Func<PreparedCodeGraphConnection, Func<CancellationToken, Task<PreparedSourceIssueResult>>,
        ICodeGraphWorkerToolSession> OpenCodeGraph);

/// <summary>Owns finite, serialized tool phases for one exact October Repository approval.</summary>
public sealed class OctoberToolRuntime : IAsyncDisposable
{
    private readonly OctoberToolRuntimeDependencies _dependencies;
    private readonly PreparedSourceApproval _approval;
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly TimeSpan _lifetime;
    private readonly CancellationTokenSource _lifetimeCancellation;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposalGate = new();
    private readonly List<PreparedToolWorkspaceLease> _jdtLeases = [];
    private PreparedCodeGraphConnection? _connection;
    private ICodeGraphWorkerToolSession? _session;
    private IPreparedJavaNavigationContext? _java;
    private int _sessionCalls;
    private OctoberCodeGraphSessionBudget _sessionBudget;
    private bool _sessionBudgetBound;
    private bool _faulted;
    private bool _cleanupFailed;
    private bool _closed;
    private Task? _disposal;
    private PreparedSourceOutcome? _sourceFailure;
    private OctoberCodeGraphOwner? _graphOwner;

    internal void UseGraphOwner(OctoberCodeGraphOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_closed || _graphOwner is not null || _connection is not null || _session is not null || _java is not null ||
            owner.RepositoryId != RepositoryId || owner.RootBindingIdentity != _approval.RootBindingIdentity)
            throw new InvalidOperationException("The original CodeGraph owner cannot be attached.");
        _graphOwner = owner;
    }
    private readonly AsyncLocal<ToolObservationScope?> _observation = new();

    public OctoberToolRuntime(PreparedSourceApproval approval, PreparedSourceBinding sourceBinding,
        PreparedToolWorkspaceIssuer workspaceIssuer, PreparedJavaNavigationContextFactory javaFactory,
        PreparedCodeGraphConnectionFactory codeGraphFactory, TimeSpan remainingRunLifetime)
        : this(approval, remainingRunLifetime, TimeProvider.System)
    {
        ArgumentNullException.ThrowIfNull(sourceBinding);
        ArgumentNullException.ThrowIfNull(workspaceIssuer);
        ArgumentNullException.ThrowIfNull(javaFactory);
        ArgumentNullException.ThrowIfNull(codeGraphFactory);
        string root = sourceBinding.GetTrustedRoot(approval);
        _dependencies = new(
            async (remaining, token) =>
            {
                PreparedSourceIssueResult before = await sourceBinding.IssueAsync(approval, Limits(remaining), token);
                await using (IPreparedSourceReadContext? grant = before.Context)
                {
                    if (before.Status != PreparedSourceIssueStatus.Issued || grant is null)
                        throw new InvalidOperationException("Current source approval is unavailable.");
                }
                TimeSpan window = Window(remaining);
                PreparedToolWorkspaceIssueResult leaseResult = await workspaceIssuer.IssueAsync(
                    new(approval, root, PreparedToolPurpose.CodeGraphPreparation, window), token);
                await using PreparedToolWorkspaceLease lease = leaseResult.Lease
                    ?? throw new InvalidOperationException("Current CodeGraph authority is unavailable.");
                if (leaseResult.Status != PreparedToolWorkspaceIssueStatus.Issued ||
                    await lease.CheckCurrentAsync(token) != PreparedToolWorkspaceCheckStatus.Current)
                    throw new InvalidOperationException("Current CodeGraph authority is unavailable.");
                using var preparation = CancellationTokenSource.CreateLinkedTokenSource(token);
                preparation.CancelAfter(window);
                PreparedCodeGraphConnection connection = await codeGraphFactory.PrepareAsync(
                    approval.RepositoryId, root, remaining, preparation.Token);
                try
                {
                    if (await lease.CheckCurrentAsync(token) != PreparedToolWorkspaceCheckStatus.Current)
                        throw new InvalidOperationException("CodeGraph preparation authority expired.");
                    PreparedSourceIssueResult after = await sourceBinding.IssueAsync(approval, Limits(remaining), connection, token);
                    await using IPreparedSourceReadContext? grant = after.Context;
                    if (after.Status != PreparedSourceIssueStatus.Issued || grant is null)
                        throw new InvalidOperationException("CodeGraph preparation binding changed.");
                    return connection;
                }
                catch
                {
                    await connection.DisposeAsync();
                    throw;
                }
            },
            (connection, remaining, token) => connection is null
                ? sourceBinding.IssueAsync(approval, Limits(remaining), token)
                : sourceBinding.IssueAsync(approval, Limits(remaining), connection, token),
            sourceBinding.ObserveFileIdentityAsync,
            async (remaining, token) =>
            {
                PreparedToolWorkspaceIssueResult issued = await workspaceIssuer.IssueAsync(
                    new(approval, root, PreparedToolPurpose.JdtNavigation, Window(remaining)), token);
                PreparedToolWorkspaceLease lease = issued.Lease
                    ?? throw new InvalidOperationException("Current JDT authority is unavailable.");
                _jdtLeases.Add(lease);
                if (issued.Status != PreparedToolWorkspaceIssueStatus.Issued)
                {
                    await lease.DisposeAsync();
                    throw new InvalidOperationException("Current JDT authority is unavailable.");
                }
                PreparedJavaNavigationOpenResult opened = await javaFactory.OpenDedicatedAsync(lease, token);
                if (opened.Status != PreparedJavaNavigationOpenStatus.Opened || opened.Context is null)
                {
                    bool candidate = opened.Status is PreparedJavaNavigationOpenStatus.ConfigurationUnavailable or PreparedJavaNavigationOpenStatus.ReadinessFailed;
                    bool current = !candidate;
                    Exception? currentnessFailure = null;
                    if (candidate)
                    {
                        try { current = await lease.CheckCurrentAsync(token) == PreparedToolWorkspaceCheckStatus.Current; }
                        catch (Exception error) { currentnessFailure = error; }
                    }
                    if (opened.Status == PreparedJavaNavigationOpenStatus.CleanupUnconfirmed)
                    {
                        // An inconsistent returned context still owns its disposal,
                        // but cannot turn unconfirmed producer cleanup into a grant.
                        if (opened.Context is not null)
                            try { await opened.Context.DisposeAsync(); }
                            catch
                            {
                                _cleanupFailed = true;
                                ObserveFailure(OctoberToolFailureBoundary.RuntimeSettlement, javaOpen: opened.Status, cleanup: false);
                                throw;
                            }
                        throw new PreparedJdtLsOpenFailureException(opened.Status, cleanupConfirmed: false);
                    }
                    try
                    {
                        if (opened.Context is not null) await opened.Context.DisposeAsync();
                        await lease.DisposeAsync();
                    }
                    catch
                    {
                        _cleanupFailed = true;
                        ObserveFailure(OctoberToolFailureBoundary.RuntimeSettlement, javaOpen: opened.Status, cleanup: false);
                        throw;
                    }
                    bool settled = lease.Lifetime == PreparedToolWorkspaceLifetime.Disposed;
                    if (!current)
                    {
                        ObserveFailure(OctoberToolFailureBoundary.SourceCurrentness, cleanup: settled);
                        if (currentnessFailure is not null)
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(currentnessFailure).Throw();
                        throw new InvalidOperationException("Current JDT authority is unavailable.");
                    }
                    if (opened.Context is not null)
                    {
                        ObserveFailure(OctoberToolFailureBoundary.RuntimeOpen, javaOpen: opened.Status, cleanup: settled);
                        throw new InvalidOperationException("Dedicated JDT result is inconsistent.");
                    }
                    throw new PreparedJdtLsOpenFailureException(opened.Status, settled);
                }
                return new OwnedJavaContext(opened.Context, lease);
            },
            (connection, openSource) => connection.OpenSession(openSource));
    }

    internal OctoberToolRuntime(PreparedSourceApproval approval, TimeSpan remainingRunLifetime,
        OctoberToolRuntimeDependencies dependencies, TimeProvider? timeProvider = null)
        : this(approval, remainingRunLifetime, timeProvider ?? TimeProvider.System)
        => _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));

    private OctoberToolRuntime(PreparedSourceApproval approval, TimeSpan remainingRunLifetime, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.RepositoryId == Guid.Empty || remainingRunLifetime <= TimeSpan.Zero ||
            remainingRunLifetime > TimeSpan.FromMinutes(30))
            throw new ArgumentOutOfRangeException(nameof(remainingRunLifetime));
        RepositoryId = approval.RepositoryId;
        _sessionBudget = new(RepositoryId);
        _approval = approval;
        _clock = clock;
        _started = clock.GetTimestamp();
        _lifetime = remainingRunLifetime;
        _lifetimeCancellation = new(remainingRunLifetime);
        _dependencies = null!;
    }

    public Guid RepositoryId { get; }
    internal void UseSessionBudget(OctoberCodeGraphSessionBudget budget)
    {
        if (_closed || _sessionBudgetBound || _connection is not null || _session is not null ||
            _java is not null || _sessionBudget.Used != 0 || budget.RepositoryId != RepositoryId)
            throw new InvalidOperationException("Native session accounting must be bound before runtime use.");
        _sessionBudget = budget;
        _sessionBudgetBound = true;
    }
    internal bool HasPreparedBinding(Guid repositoryId, string rootBindingIdentity, string preparedVersion)
        => repositoryId == _approval.RepositoryId &&
            string.Equals(rootBindingIdentity, _approval.RootBindingIdentity, StringComparison.Ordinal) &&
            string.Equals(preparedVersion, _approval.ApprovalIdentity, StringComparison.Ordinal);
    internal PreparedCodeGraphConnection? PreparedConnection => _connection;
    internal IReadOnlyList<PreparedToolWorkspaceLease> JdtLeases => _jdtLeases;
    internal IDisposable ObserveInvocation(SourceMetadataOperation operation, Action<OctoberToolDiagnosticSnapshot> observe)
    {
        var scope = new ToolObservationScope(this, _observation.Value, operation, observe);
        _observation.Value = scope;
        return scope;
    }

    private void ObserveFailure(OctoberToolFailureBoundary boundary, PreparedJavaNavigationOpenStatus? javaOpen = null,
        PreparedJavaNavigationOutcome? javaOutcome = null, PreparedSourceOutcome? sourceOutcome = null,
        CodeGraphToolInvocationStatus? graphStatus = null, bool? cleanup = null) =>
        _observation.Value?.Record(boundary, javaOpen, javaOutcome, sourceOutcome, graphStatus, cleanup);

    private sealed class ToolObservationScope(OctoberToolRuntime owner, ToolObservationScope? previous,
        SourceMetadataOperation operation, Action<OctoberToolDiagnosticSnapshot> observe) : IDisposable
    {
        private readonly object _sync = new();
        private bool _closed;
        internal void Record(OctoberToolFailureBoundary boundary, PreparedJavaNavigationOpenStatus? javaOpen,
            PreparedJavaNavigationOutcome? javaOutcome, PreparedSourceOutcome? sourceOutcome,
            CodeGraphToolInvocationStatus? graphStatus, bool? cleanup)
        {
            lock (_sync)
                if (!_closed) observe(new(operation, boundary,
                    javaOpen is { } opened && Enum.IsDefined(opened) ? opened : null,
                    javaOutcome is { } java && Enum.IsDefined(java) ? java : null,
                    sourceOutcome is { } source && Enum.IsDefined(source) ? source : null,
                    graphStatus is { } graph && Enum.IsDefined(graph) ? graph : null, cleanup));
        }
        public void Dispose()
        {
            lock (_sync) { if (_closed) return; _closed = true; }
            owner._observation.Value = previous;
        }
    }
    private TimeSpan Remaining => _lifetime - _clock.GetElapsedTime(_started);
    private bool Available => !_closed && !_faulted && Remaining > TimeSpan.Zero && !_lifetimeCancellation.IsCancellationRequested;

    public Task<CodeGraphToolReadiness> PrepareAsync(CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            await EnsurePreparedAsync(token);
            CodeGraphToolReadiness readiness = await EnsureSessionAsync(token);
            // New-file tool registration also discovers the cached native session.
            // Renew its bounded lifetime just as ExploreAsync does, never before
            // old-session settlement or outside the original runtime lifetime.
            if (readiness.Status == CodeGraphToolReadinessStatus.Expired && Available)
            {
                await CloseSessionAsync();
                token.ThrowIfCancellationRequested();
                if (Available) readiness = await EnsureSessionAsync(token);
            }
            return readiness;
        }, cancelled => new CodeGraphToolReadiness(cancelled ? CodeGraphToolReadinessStatus.Cancelled : CodeGraphToolReadinessStatus.Unavailable,
            RepositoryId, CodeGraphToolStage.Preparation, cancelled ? CodeGraphToolIssueCode.Cancelled : CodeGraphToolIssueCode.SourceUnprepared), cancellationToken);

    public Task<CodeGraphToolInvocationResult> ExploreAsync(string query, int maxFiles,
        CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            await EnsurePreparedAsync(token);
            if (_sessionCalls >= 10) await CloseSessionAsync();
            for (int renewal = 0; renewal <= 1; renewal++)
            {
                CodeGraphToolReadiness readiness = await EnsureSessionAsync(token);
                if (readiness.Status == CodeGraphToolReadinessStatus.Expired && renewal == 0 && Available)
                {
                    await CloseSessionAsync();
                    continue;
                }
                if (!readiness.IsReady)
                {
                    ObserveFailure(OctoberToolFailureBoundary.RuntimeOpen);
                    _faulted = true;
                    return GraphFailure(false);
                }
                _sessionCalls++;
                CodeGraphToolInvocationResult result;
                try { result = await _session!.ExploreAsync(query, maxFiles, token); }
                catch { ObserveFailure(OctoberToolFailureBoundary.RuntimeInvoke); throw; }
                if (result.Status == CodeGraphToolInvocationStatus.Expired && renewal == 0 && Available)
                {
                    await CloseSessionAsync();
                    continue;
                }
                if (result.RepositoryId != RepositoryId)
                {
                    ObserveFailure(OctoberToolFailureBoundary.SourceCurrentness, graphStatus: result.Status);
                    _faulted = true;
                    return GraphFailure(token.IsCancellationRequested);
                }
                if (!Available || token.IsCancellationRequested)
                    return GraphFailure(token.IsCancellationRequested);
                if (result.Status is not (CodeGraphToolInvocationStatus.Complete or
                    CodeGraphToolInvocationStatus.NoMatch or CodeGraphToolInvocationStatus.Limited))
                    _faulted = true;
                return result;
            }
            return GraphFailure(false);
        }, GraphFailure, cancellationToken);

    public Task<PreparedJavaNavigationResult> NavigateAsync(PreparedJavaNavigationOperation operation,
        PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            await CloseSessionAsync();
            try { _java = await _dependencies.OpenDedicatedJava(Window(Remaining), token); }
            catch (PreparedJdtLsOpenFailureException failure)
            {
                bool candidate = failure.CleanupConfirmed && failure.Status is
                    PreparedJavaNavigationOpenStatus.ConfigurationUnavailable or PreparedJavaNavigationOpenStatus.ReadinessFailed;
                if (candidate && Available && !token.IsCancellationRequested)
                {
                    PreparedSourceIssueResult current;
                    try { current = await OpenSourceAsync(token); }
                    catch
                    {
                        ObserveFailure(OctoberToolFailureBoundary.SourceCurrentness, cleanup: true);
                        _faulted = true;
                        throw;
                    }
                    bool verified = current.Status == PreparedSourceIssueStatus.Issued && current.Context?.RepositoryId == RepositoryId &&
                        current.Context.Lifetime == PreparedSourceContextLifetime.Open && Available && !token.IsCancellationRequested;
                    if (!verified) ObserveFailure(OctoberToolFailureBoundary.SourceCurrentness, cleanup: true);
                    try { if (current.Context is not null) await current.Context.DisposeAsync(); }
                    catch
                    {
                        ObserveFailure(OctoberToolFailureBoundary.RuntimeSettlement, cleanup: false);
                        _cleanupFailed = true; _faulted = true;
                        throw;
                    }
                    if (verified && Available && !token.IsCancellationRequested)
                    {
                        bool configuration = failure.Status == PreparedJavaNavigationOpenStatus.ConfigurationUnavailable;
                        return new PreparedJavaNavigationResult(RepositoryId, operation,
                            configuration ? PreparedJavaNavigationOutcome.ConfigurationUnavailable : PreparedJavaNavigationOutcome.ReadinessFailed,
                            [], [new(configuration ? "jdt_configuration_unavailable" : "jdt_readiness_failed", null)], null);
                    }
                    _faulted = true;
                    return JavaFailure(operation, token.IsCancellationRequested);
                }
                ObserveFailure(OctoberToolFailureBoundary.RuntimeOpen, javaOpen: failure.Status, cleanup: failure.CleanupConfirmed);
                if (!failure.CleanupConfirmed) _cleanupFailed = true;
                _faulted = true;
                throw;
            }
            catch
            {
                ObserveFailure(OctoberToolFailureBoundary.RuntimeOpen);
                _faulted = true;
                throw;
            }
            PreparedJavaNavigationResult result;
            try
            {
                if (_java.RepositoryId != RepositoryId) throw new InvalidOperationException("JDT Repository binding changed.");
                result = operation switch
                {
                    PreparedJavaNavigationOperation.Definition => await _java.GetDefinitionAsync(request, token),
                    PreparedJavaNavigationOperation.References => await _java.GetReferencesAsync(request, token),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation)),
                };
            }
            catch { ObserveFailure(OctoberToolFailureBoundary.RuntimeInvoke); throw; }
            finally
            {
                try { await _java.DisposeAsync(); _java = null; }
                catch { ObserveFailure(OctoberToolFailureBoundary.RuntimeSettlement, cleanup: false); _cleanupFailed = true; _faulted = true; throw; }
            }
            if (result.RepositoryId != RepositoryId)
            {
                ObserveFailure(OctoberToolFailureBoundary.SourceCurrentness, javaOutcome: result.Outcome, cleanup: true);
                _faulted = true;
            }
            return Available && !token.IsCancellationRequested && result.RepositoryId == RepositoryId
                ? result : JavaFailure(operation, token.IsCancellationRequested);
        }, cancelled => JavaFailure(operation, cancelled), cancellationToken);

    public Task<PreparedSourceResult> ListAsync(PreparedSourceListRequest request,
        CancellationToken cancellationToken = default)
        => RunAsync(token => SequenceAsync(PreparedSourceOperation.List, request.Continuation,
            (context, cursor, ct) => context.ListAsync(request with { Continuation = cursor }, ct), token),
            cancelled => SourceFailure(PreparedSourceOperation.List, cancelled), cancellationToken);

    public Task<PreparedSourceResult> SearchTextAsync(PreparedSourceSearchTextRequest request,
        CancellationToken cancellationToken = default)
        => RunAsync(token => SequenceAsync(PreparedSourceOperation.SearchText, request.Continuation,
            (context, cursor, ct) => context.SearchTextAsync(request with { Continuation = cursor }, ct), token),
            cancelled => SourceFailure(PreparedSourceOperation.SearchText, cancelled), cancellationToken);

    public Task<PreparedSourceResult> ReadTextRangeAsync(PreparedSourceReadTextRangeRequest request,
        CancellationToken cancellationToken = default)
        => RunAsync(async token =>
        {
            await EnsurePreparedAsync(token);
            PreparedSourceIssueResult issued = await OpenSourceAsync(token);
            await using IPreparedSourceReadContext? context = issued.Context;
            if (issued.Status != PreparedSourceIssueStatus.Issued || context?.RepositoryId != RepositoryId)
                return SourceOutcome(PreparedSourceOperation.ReadTextRange, MapIssue(issued.Status));
            PreparedSourceResult result = await context.ReadTextRangeAsync(request, token);
            if (result.Outcome is PreparedSourceOutcome.Denied or PreparedSourceOutcome.AuthorityUnavailable or PreparedSourceOutcome.ChangedInput)
                return SourceOutcome(PreparedSourceOperation.ReadTextRange, result.Outcome);
            return Available && !token.IsCancellationRequested && result.RepositoryId == RepositoryId
                ? result : SourceFailure(PreparedSourceOperation.ReadTextRange, token.IsCancellationRequested);
        }, cancelled => SourceFailure(PreparedSourceOperation.ReadTextRange, cancelled), cancellationToken);

    public Task<OctoberSourceFrameResult> AcquireFileFrameAsync(string relativePath,
        OctoberSourceFrameLimits? limits = null, CancellationToken cancellationToken = default)
    {
        OctoberSourceFrameLimits bound = limits ?? new();
        if (!OctoberPreparedSourceFrame.Valid(relativePath, bound))
            return Task.FromResult(new OctoberSourceFrameResult(OctoberSourceFrameStatus.InvalidInput));
        return RunAsync(async token =>
        {
            await EnsurePreparedAsync(token);
            return await OctoberPreparedSourceFrame.AcquireAsync(this, _approval, relativePath, bound,
                OpenSourceAsync, _dependencies.ObserveIdentity, token);
        }, cancelled => new OctoberSourceFrameResult(cancelled ? OctoberSourceFrameStatus.Cancelled : OctoberSourceFrameStatus.Unavailable),
            cancellationToken);
    }

    internal async Task<bool> VerifyFrameAsync(OctoberPreparedSourceFrame frame, OctoberSourceFrameLimits limits,
        CancellationToken cancellationToken)
    {
        if (frame.Origin.RepositoryId != RepositoryId || frame.Origin.RootBindingIdentity != _approval.RootBindingIdentity ||
            frame.Origin.PreparedVersion != _approval.ApprovalIdentity) return false;
        OctoberSourceFrameResult current = await AcquireFileFrameAsync(frame.Origin.RelativePath, limits, cancellationToken);
        return current.Status == OctoberSourceFrameStatus.Acquired && current.Frame is { } observed &&
            observed.Origin == frame.Origin && observed.ByteLength == frame.ByteLength &&
            observed.LineCount == frame.LineCount && observed.FileIdentity == frame.FileIdentity;
    }

    private async Task<PreparedSourceResult> SequenceAsync(PreparedSourceOperation operation, string? incomingCursor,
        Func<IPreparedSourceReadContext, string?, CancellationToken, Task<PreparedSourceResult>> getPage,
        CancellationToken token)
    {
        if (incomingCursor is not null) return SourceOutcome(operation, PreparedSourceOutcome.Denied);
        await EnsurePreparedAsync(token);
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        var entries = new Dictionary<string, PreparedSourceEntry>(StringComparer.Ordinal);
        var matches = new Dictionary<(string Path, long Start, int Length), PreparedSourceMatch>();
        var limitations = new HashSet<PreparedSourceLimitation>();
        long returnedBytes = 0;
        for (int restart = 0; restart <= 1; restart++)
        {
            PreparedSourceIssueResult issued = await OpenSourceAsync(token);
            await using IPreparedSourceReadContext? context = issued.Context;
            if (issued.Status != PreparedSourceIssueStatus.Issued || context?.RepositoryId != RepositoryId)
            {
                if (issued.Status == PreparedSourceIssueStatus.DeadlineExceeded)
                {
                    if (restart == 0 && Available) continue;
                    return SourceOutcome(operation, PreparedSourceOutcome.Incomplete);
                }
                return SourceOutcome(operation, MapIssue(issued.Status));
            }
            var seenThisPass = new HashSet<string>(StringComparer.Ordinal);
            var cursors = new HashSet<string>(StringComparer.Ordinal);
            string? cursor = null;
            bool expired = false;
            try
            {
            for (int pageNumber = 0; pageNumber < PreparedSourceGrantLimits.Default.MaximumOperations; pageNumber++)
            {
                PreparedSourceResult page = await getPage(context, cursor, token);
                if (page.Outcome == PreparedSourceOutcome.DeadlineExceeded)
                {
                    expired = true;
                    break;
                }
                if (page.RepositoryId != RepositoryId || page.Operation != operation)
                    return SourceOutcome(operation, PreparedSourceOutcome.Denied);
                if (page.Outcome is PreparedSourceOutcome.Denied or PreparedSourceOutcome.AuthorityUnavailable or PreparedSourceOutcome.ChangedInput or PreparedSourceOutcome.Cancelled)
                    return SourceOutcome(operation, page.Outcome);
                foreach (PreparedSourceLimitation limitation in page.Limitations)
                {
                    if (limitation.Location.RepositoryId != RepositoryId)
                        return SourceOutcome(operation, PreparedSourceOutcome.Denied);
                    if (limitations.Add(limitation)) returnedBytes += Encoding.UTF8.GetByteCount(limitation.Location.RelativePath) + 32;
                }
                if (limitations.Count > PreparedSourceGrantLimits.Default.MaximumMetadataEntries ||
                    returnedBytes > PreparedSourceGrantLimits.Default.MaximumReturnedBytes)
                    return SourceOutcome(operation, PreparedSourceOutcome.Incomplete);
                if (page.Outcome is not (PreparedSourceOutcome.Complete or PreparedSourceOutcome.CompleteEmpty or PreparedSourceOutcome.Limited))
                {
                    if (page.Outcome is PreparedSourceOutcome.Denied or PreparedSourceOutcome.AuthorityUnavailable or PreparedSourceOutcome.ChangedInput)
                        _faulted = true;
                    return SourceOutcome(operation, page.Outcome) with { Limitations = limitations.ToArray() };
                }
                foreach (PreparedSourceEntry entry in page.Entries)
                {
                    string path = entry.Location.RelativePath;
                    if (entry.Location.RepositoryId != RepositoryId) return SourceOutcome(operation, PreparedSourceOutcome.Denied);
                    seenThisPass.Add(path);
                    if (entry.Kind == PreparedSourceEntryKind.RegularFile &&
                        await CheckIdentityAsync(context, path, identities, token) is { } identityFailure)
                        return SourceOutcome(operation, identityFailure);
                    if (entries.TryGetValue(path, out PreparedSourceEntry? previous) && previous != entry)
                        return SourceOutcome(operation, PreparedSourceOutcome.ChangedInput);
                    if (entries.TryAdd(path, entry)) returnedBytes += Encoding.UTF8.GetByteCount(path) + 32;
                }
                foreach (PreparedSourceMatch match in page.Matches)
                {
                    string path = match.Location.RelativePath;
                    if (match.Location.RepositoryId != RepositoryId) return SourceOutcome(operation, PreparedSourceOutcome.Denied);
                    seenThisPass.Add(path);
                    if (await CheckIdentityAsync(context, path, identities, token) is { } identityFailure)
                        return SourceOutcome(operation, identityFailure);
                    if (matches.TryAdd((path, match.Location.ByteStart, match.Location.ByteLength), match))
                        returnedBytes += Encoding.UTF8.GetByteCount(path) + Encoding.UTF8.GetByteCount(match.Excerpt ?? string.Empty) + 64;
                }
                if (entries.Count + matches.Count + limitations.Count > PreparedSourceGrantLimits.Default.MaximumMetadataEntries ||
                    returnedBytes > PreparedSourceGrantLimits.Default.MaximumReturnedBytes)
                    return SourceOutcome(operation, PreparedSourceOutcome.Incomplete);
                cursor = page.Continuation;
                if (cursor is not null)
                {
                    if (!cursors.Add(cursor)) return SourceOutcome(operation, PreparedSourceOutcome.Incomplete);
                    continue;
                }
                if (page.Outcome == PreparedSourceOutcome.Limited)
                    return SourceOutcome(operation, PreparedSourceOutcome.Incomplete) with { Limitations = limitations.ToArray() };
                if (identities.Keys.Concat(entries.Keys).Any(path => !seenThisPass.Contains(path)))
                    return SourceOutcome(operation, PreparedSourceOutcome.ChangedInput);
                foreach (string path in identities.Keys)
                    if (await CheckIdentityAsync(context, path, identities, token) is { } identityFailure)
                        return SourceOutcome(operation, identityFailure);
                if (!Available || token.IsCancellationRequested) return SourceFailure(operation, token.IsCancellationRequested);
                return new PreparedSourceResult
                {
                    RepositoryId = RepositoryId, Operation = operation,
                    Outcome = entries.Count + matches.Count == 0 ? PreparedSourceOutcome.CompleteEmpty : PreparedSourceOutcome.Complete,
                    Entries = entries.Values.OrderBy(entry => entry.Location.RelativePath, StringComparer.Ordinal).ToArray(),
                    Matches = matches.Values.OrderBy(match => match.Location.RelativePath, StringComparer.Ordinal).ThenBy(match => match.Location.ByteStart).ToArray(),
                    Limitations = limitations.ToArray(),
                };
            }
            }
            catch (SourceGrantExpiredException) { expired = true; }
            if (!expired || restart == 1) return SourceOutcome(operation, PreparedSourceOutcome.Incomplete);
        }
        return SourceOutcome(operation, PreparedSourceOutcome.Incomplete);
    }

    private sealed class SourceGrantExpiredException : Exception { }

    private async Task<PreparedSourceOutcome?> CheckIdentityAsync(IPreparedSourceReadContext source, string path,
        Dictionary<string, string> identities, CancellationToken token)
    {
        string? observed = await _dependencies.ObserveIdentity(source, path, token);
        if (observed is null)
        {
            if (source.Lifetime == PreparedSourceContextLifetime.ExpiredOrCancelled)
                throw new SourceGrantExpiredException();
            return source.Lifetime == PreparedSourceContextLifetime.AuthorityClosed
                ? PreparedSourceOutcome.Denied : PreparedSourceOutcome.Incomplete;
        }
        if (identities.TryGetValue(path, out string? previous))
            return previous == observed ? null : PreparedSourceOutcome.ChangedInput;
        identities.Add(path, observed);
        return null;
    }

    private async Task EnsurePreparedAsync(CancellationToken token)
    {
        if (_connection is not null) return;
        _connection = _graphOwner is null
            ? await _dependencies.PrepareCodeGraph(Remaining, token)
            : await _graphOwner.GetOrCreateAsync(_dependencies.PrepareCodeGraph, token);
        if (_connection.RepositoryId != RepositoryId || !Available)
            throw new InvalidOperationException("Prepared CodeGraph binding is unavailable.");
    }

    private Task<PreparedSourceIssueResult> OpenSourceAsync(CancellationToken token)
        => _dependencies.IssueSource(_connection, Window(Remaining), token);

    private async Task<CodeGraphToolReadiness> EnsureSessionAsync(CancellationToken token)
    {
        if (_session is null)
        {
            if (!_sessionBudget.TryReserve()) throw new InvalidOperationException("CodeGraph session replacements are exhausted.");
            _session = _dependencies.OpenCodeGraph(_connection!, OpenSourceAsync);
            _sessionCalls = 0;
        }
        return await _session.DiscoverAsync(token);
    }

    private async Task CloseSessionAsync()
    {
        if (_session is null) return;
        try { await _session.DisposeAsync(); _session = null; }
        catch { ObserveFailure(OctoberToolFailureBoundary.RuntimeSettlement, cleanup: false); _cleanupFailed = true; _faulted = true; throw; }
    }

    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation,
        Func<bool, T> failure, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
        try { await _gate.WaitAsync(linked.Token); }
        catch (OperationCanceledException) { return failure(true); }
        try
        {
            if (!Available) return failure(linked.IsCancellationRequested);
            return await operation(linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return failure(true); }
        catch (Exception) { ObserveFailure(OctoberToolFailureBoundary.RuntimeInvoke); _faulted = true; return failure(false); }
        finally { _gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposalGate)
        {
            if (_disposal is not null) return new(_disposal);
            _closed = true;
            _lifetimeCancellation.Cancel();
            _disposal = DisposeCoreAsync();
            return new(_disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CloseSessionAsync();
            if (_java is not null) await _java.DisposeAsync();
            if (_jdtLeases.Any(lease => lease.Lifetime != PreparedToolWorkspaceLifetime.Disposed))
                _cleanupFailed = true;
            if (_cleanupFailed) throw new InvalidOperationException("Tool cleanup remains unconfirmed.");
            // Borrowed state survives approval replacement; this runtime has
            // already settled its native session and JDT resources above.
            if (_connection is not null && _graphOwner is null) await _connection.DisposeAsync();
        }
        finally { _gate.Release(); }
    }

    private static TimeSpan Window(TimeSpan remaining) => remaining < TimeSpan.FromSeconds(30) ? remaining : TimeSpan.FromSeconds(30);
    private static PreparedSourceGrantLimits Limits(TimeSpan remaining) => PreparedSourceGrantLimits.Default with { Deadline = Window(remaining) };
    private CodeGraphToolInvocationResult GraphFailure(bool cancelled) => new()
    {
        RepositoryId = RepositoryId, Status = cancelled ? CodeGraphToolInvocationStatus.Cancelled : CodeGraphToolInvocationStatus.Unavailable,
    };
    private PreparedJavaNavigationResult JavaFailure(PreparedJavaNavigationOperation operation, bool cancelled)
        => new(RepositoryId, operation, cancelled ? PreparedJavaNavigationOutcome.Cancelled : PreparedJavaNavigationOutcome.ReadinessFailed, [], [], null);
    private PreparedSourceResult SourceFailure(PreparedSourceOperation operation, bool cancelled)
        => SourceOutcome(operation, cancelled ? PreparedSourceOutcome.Cancelled : _sourceFailure ?? PreparedSourceOutcome.AuthorityUnavailable);
    private PreparedSourceResult SourceOutcome(PreparedSourceOperation operation, PreparedSourceOutcome outcome)
    {
        if (outcome is PreparedSourceOutcome.Denied or PreparedSourceOutcome.AuthorityUnavailable or PreparedSourceOutcome.ChangedInput)
        {
            _faulted = true;
            _sourceFailure = outcome;
        }
        return new() { RepositoryId = RepositoryId, Operation = operation, Outcome = outcome };
    }

    private static PreparedSourceOutcome MapIssue(PreparedSourceIssueStatus status) => status switch
    {
        PreparedSourceIssueStatus.Denied => PreparedSourceOutcome.Denied,
        PreparedSourceIssueStatus.AuthorityUnavailable => PreparedSourceOutcome.AuthorityUnavailable,
        PreparedSourceIssueStatus.RootUnavailable => PreparedSourceOutcome.ChangedInput,
        PreparedSourceIssueStatus.Cancelled => PreparedSourceOutcome.Cancelled,
        PreparedSourceIssueStatus.DeadlineExceeded => PreparedSourceOutcome.DeadlineExceeded,
        _ => PreparedSourceOutcome.Incomplete,
    };

    private sealed class OwnedJavaContext(IPreparedJavaNavigationContext context, PreparedToolWorkspaceLease lease)
        : IPreparedJavaNavigationContext
    {
        public Guid RepositoryId => context.RepositoryId;
        public Task<PreparedJavaNavigationResult> GetDefinitionAsync(PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default)
            => context.GetDefinitionAsync(request, cancellationToken);
        public Task<PreparedJavaNavigationResult> GetReferencesAsync(PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default)
            => context.GetReferencesAsync(request, cancellationToken);
        public async ValueTask DisposeAsync() { await context.DisposeAsync(); await lease.DisposeAsync(); }
    }
}
