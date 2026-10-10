using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;

namespace Vulgata.OctoberGenerator;

internal delegate OctoberToolRuntime OctoberRuntimeConstructor(
    PreparedSourceApproval approval,
    PreparedSourceBinding sourceBinding,
    PreparedToolWorkspaceIssuer workspaceIssuer,
    PreparedJavaNavigationContextFactory javaFactory,
    PreparedCodeGraphConnectionFactory codeGraphFactory,
    TimeSpan remainingLifetime);

/// <summary>Creates owned per-Repository runtimes after Begin under one original remaining lifetime.</summary>
internal sealed class OctoberScanRunSourceFactory
{
    private readonly OctoberRuntimeConfiguration _configuration;
    private readonly IOctoberLocalAuthority _authority;
    private readonly OctoberRuntimeConstructor _createRuntime;
    private readonly TimeProvider _clock;

    internal OctoberScanRunSourceFactory(OctoberRuntimeConfiguration configuration,
        IOctoberLocalAuthority authority, TimeProvider? timeProvider = null)
        : this(configuration, authority, static (approval, binding, workspace, java, graph, remaining) =>
            new OctoberToolRuntime(approval, binding, workspace, java, graph, remaining), timeProvider) { }

    internal OctoberScanRunSourceFactory(OctoberRuntimeConfiguration configuration,
        IOctoberLocalAuthority authority, OctoberRuntimeConstructor createRuntime,
        TimeProvider? timeProvider = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _createRuntime = createRuntime ?? throw new ArgumentNullException(nameof(createRuntime));
        _clock = timeProvider ?? TimeProvider.System;
    }

    internal bool ValidateSelection(OctoberGenerateExecution execution)
    {
        if (execution is null || !_configuration.IsValid() || execution.Session is null ||
            execution.Setup?.Snapshot?.Request is not { } request || execution.Authority is not { } selection ||
            selection.SelectedContext is not { } context || request.Repositories.Count == 0 ||
            execution.Session.ExpiresAtUtc <= _clock.GetUtcNow() ||
            execution.Session.SubjectId != selection.SubjectId ||
            execution.Session.ManagementAuthorityId != context.ManagementAuthorityId ||
            execution.Setup.TargetId != request.TargetId || execution.Setup.BatchId != request.BatchId ||
            request.TargetId != selection.TargetId || request.BatchId != selection.BatchId ||
            request.SelectionDigest != selection.SelectionDigest ||
            request.Repositories.Count != context.Repositories.Length ||
            request.Repositories.Count != selection.RepositoryIds.Count ||
            request.Repositories.Select(repository => repository.RepositoryId).Distinct().Count() != request.Repositories.Count ||
            _configuration.PreparedRoots.Length != request.Repositories.Count)
            return false;
        return request.Repositories.All(repository => repository.RepositoryId != Guid.Empty &&
            context.Repositories.Count(selected => selected == repository) == 1 &&
            selection.RepositoryIds.Count(id => id == repository.RepositoryId) == 1 &&
            _configuration.PreparedRoots.Count(root => root.TargetId == request.TargetId &&
                root.BatchId == request.BatchId && root.RepositoryId == repository.RepositoryId &&
                root.RootBindingIdentity == repository.RootBindingIdentity) == 1);
    }

    internal async Task<IOctoberScanRunSource> OpenAsync(OctoberGenerateExecution execution,
        TimeSpan remainingRunLifetime, CancellationToken cancellationToken = default)
    {
        if (!ValidateSelection(execution) || remainingRunLifetime <= TimeSpan.Zero ||
            remainingRunLifetime > TimeSpan.FromMinutes(30))
            throw new InvalidOperationException("The exact source configuration is unavailable.");
        long started = _clock.GetTimestamp();
        var runtimes = new Dictionary<Guid, RuntimeBinding>();
        var graphOwners = new List<OctoberCodeGraphOwner>();
        TimeSpan RunRemaining() => Min(remainingRunLifetime - _clock.GetElapsedTime(started),
            execution.Session.ExpiresAtUtc - _clock.GetUtcNow());
        using var opening = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        opening.CancelAfter(remainingRunLifetime);
        try
        {
            foreach (OctoberRepositorySelection selected in execution.Setup.Snapshot.Request.Repositories)
            {
                opening.Token.ThrowIfCancellationRequested();
                var graphOwner = new OctoberCodeGraphOwner(selected.RepositoryId, selected.RootBindingIdentity, RunRemaining);
                graphOwners.Add(graphOwner);
                runtimes.Add(selected.RepositoryId, await IssueRuntimeAsync(execution, selected,
                    new(selected.RepositoryId), graphOwner, RunRemaining, TimeSpan.Zero, null, opening.Token));
            }
            opening.Token.ThrowIfCancellationRequested();
            return new Source(this, execution, runtimes, RunRemaining);
        }
        catch
        {
            await DisposeResourcesAsync(runtimes.Values, graphOwners);
            throw;
        }
    }

    private sealed record RuntimeBinding(OctoberRepositorySelection Selection, PreparedSourceApproval Approval,
        DateTimeOffset Expires, long Started, TimeSpan Lifetime, OctoberToolRuntime Runtime,
        OctoberCodeGraphSessionBudget Budget, OctoberCodeGraphOwner GraphOwner);

    private async Task<RuntimeBinding> IssueRuntimeAsync(OctoberGenerateExecution execution,
        OctoberRepositorySelection selected, OctoberCodeGraphSessionBudget budget, OctoberCodeGraphOwner graphOwner, Func<TimeSpan> runRemaining,
        TimeSpan requiredLifetime, Func<CancellationToken, Task>? verifyPrevious, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (runRemaining() <= TimeSpan.Zero) throw new OctoberSourceWindowException(OctoberSourceWindowFailure.Expired);
        Task<OctoberSourceApprovalResult> issuing = _authority.IssueSourceApprovalAsync(
            execution.Session, execution.Setup, selected.RepositoryId, token);
        OctoberSourceApprovalResult issued;
        try { issued = await issuing.WaitAsync(token); }
        catch { _ = ObserveAsync(issuing); throw; }
        token.ThrowIfCancellationRequested();
        if (issued.Status != OctoberSourceApprovalStatus.Issued || issued.Approval is not { } approval ||
            issued.ExpiresAtUtc is not { } expires || expires <= _clock.GetUtcNow() ||
            approval.BatchId != execution.Setup.BatchId || approval.RepositoryId != selected.RepositoryId ||
            approval.SubjectId != execution.Session.SubjectId || approval.Purpose != PreparedSourcePurpose.WorkerSourceRead ||
            approval.RootBindingIdentity != selected.RootBindingIdentity || approval.OperatorLabel != selected.OperatorLabel ||
            string.IsNullOrWhiteSpace(approval.ApprovalIdentity))
            throw new OctoberSourceWindowException(OctoberSourceWindowFailure.BindingMismatch);
        // Issuance rechecks live rights but does not check the old approval. Refuse
        // revocation/expiry races before constructing anything for the replacement.
        if (verifyPrevious is not null) await verifyPrevious(token);
        TimeSpan Remaining() => Min(runRemaining(), expires - _clock.GetUtcNow());
        void RequireWindow()
        {
            TimeSpan remaining = Remaining();
            if (remaining <= TimeSpan.Zero) throw new OctoberSourceWindowException(OctoberSourceWindowFailure.Expired);
            if (remaining < Min(requiredLifetime, runRemaining()))
                throw new OctoberSourceWindowException(OctoberSourceWindowFailure.InsufficientLifetime);
        }
        RequireWindow();
        bool CurrentBinding(PreparedSourceApproval candidate) => candidate == approval && Remaining() > TimeSpan.Zero;
        var verifier = new OctoberPreparedSourceAuthorityVerifier(execution.Setup.TargetId,
            async (target, candidate, callToken) =>
            {
                if (!CurrentBinding(candidate)) return new(PreparedSourceAuthorityStatus.Revoked, null);
                var result = await _authority.VerifySourceApprovalAsync(execution.Session, target, candidate, callToken);
                callToken.ThrowIfCancellationRequested();
                return CurrentBinding(candidate) ? result : new(PreparedSourceAuthorityStatus.Revoked, null);
            }, CurrentBinding);
        var binding = new PreparedSourceBinding(new PreparedSourceContextIssuer(verifier), execution.Setup.TargetId, _configuration.PreparedRoots);
        var workspace = new PreparedToolWorkspaceIssuer(verifier, new ToolAuthorization(approval, verifier));
        var java = new PreparedJavaNavigationContextFactory(_configuration.JavaNavigation, _configuration.JavaHostPolicy);
        var graph = new PreparedCodeGraphConnectionFactory(_configuration.CodeGraphCommandPath);
        long started = _clock.GetTimestamp();
        TimeSpan lifetime = Remaining();
        OctoberToolRuntime runtime = _createRuntime(approval, binding, workspace, java, graph, lifetime);
        try
        {
            runtime.UseSessionBudget(budget);
            runtime.UseGraphOwner(graphOwner);
            token.ThrowIfCancellationRequested();
            RequireWindow();
            if (!runtime.HasPreparedBinding(selected.RepositoryId, approval.RootBindingIdentity, approval.ApprovalIdentity))
                throw new OctoberSourceWindowException(OctoberSourceWindowFailure.BindingMismatch);
            return new(selected, approval, expires, started, lifetime, runtime, budget, graphOwner);
        }
        catch
        {
            try { await runtime.DisposeAsync(); }
            catch { throw new OctoberSourceWindowException(OctoberSourceWindowFailure.CleanupUnconfirmed); }
            throw;
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;

    private static async Task ObserveAsync(Task task)
    {
        try { await task; } catch (Exception) { }
    }

    private static async Task DisposeResourcesAsync(IEnumerable<RuntimeBinding> bindings,
        IEnumerable<OctoberCodeGraphOwner>? graphOwners = null)
    {
        RuntimeBinding[] owned = bindings.ToArray();
        bool failed = false;
        foreach (RuntimeBinding value in owned)
        {
            try { await value.Runtime.DisposeAsync(); }
            catch (Exception) { failed = true; }
        }
        // Retire original descriptors only after all approval-bound borrowers.
        // Still attempt owner cleanup when a borrower could not confirm settlement.
        foreach (OctoberCodeGraphOwner owner in (graphOwners ?? owned.Select(value => value.GraphOwner)).Distinct())
        {
            try { await owner.DisposeAsync(); }
            catch (Exception) { failed = true; }
        }
        if (failed) throw new InvalidOperationException("Owned source cleanup could not be confirmed.");
    }

    private sealed class ToolAuthorization(PreparedSourceApproval approval,
        IPreparedSourceCurrentAuthorityVerifier verifier) : IPreparedToolUseAuthorizer
    {
        public async Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(PreparedSourceApproval candidate,
            PreparedToolPurpose purpose, CancellationToken cancellationToken = default)
        {
            if (candidate != approval || purpose is not (PreparedToolPurpose.JdtNavigation or PreparedToolPurpose.CodeGraphPreparation))
                return new(PreparedToolUseAuthorityStatus.Denied);
            PreparedSourceAuthorityResult result = await verifier.VerifyCurrentAsync(candidate, cancellationToken);
            return new(result.Status == PreparedSourceAuthorityStatus.Current
                ? PreparedToolUseAuthorityStatus.Current : result.Status == PreparedSourceAuthorityStatus.Unavailable
                    ? PreparedToolUseAuthorityStatus.Unavailable : PreparedToolUseAuthorityStatus.Denied);
        }
    }

    private sealed class Source(OctoberScanRunSourceFactory owner, OctoberGenerateExecution execution,
        Dictionary<Guid, RuntimeBinding> runtimes, Func<TimeSpan> runRemaining) : IOctoberScanRunSource
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Task? _disposal;
        private volatile bool _closed;
        private volatile bool _renewing;
        private volatile bool _faulted;
        // A configured file deadline is an upper bound, not permission to outlive
        // the issuer's shorter grant. Keep the initial ceiling across replacements.
        private readonly TimeSpan _grantWindow = runtimes.Values.Min(value => value.Lifetime) - TimeSpan.FromSeconds(30);

        private void RequireOpen(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_closed || _faulted) throw new OctoberSourceWindowException(OctoberSourceWindowFailure.Closed);
            if (runRemaining() <= TimeSpan.Zero) throw new OctoberSourceWindowException(OctoberSourceWindowFailure.Expired);
        }
        private TimeSpan Remaining(RuntimeBinding value) => Min(runRemaining(), Min(value.Expires - owner._clock.GetUtcNow(),
            value.Lifetime - owner._clock.GetElapsedTime(value.Started)));
        private async Task VerifyOldAsync(RuntimeBinding value, CancellationToken token)
        {
            RequireOpen(token);
            if (Remaining(value) <= TimeSpan.Zero) throw new OctoberSourceWindowException(OctoberSourceWindowFailure.Expired);
            var result = await owner._authority.VerifySourceApprovalAsync(execution.Session, execution.Setup.TargetId, value.Approval, token);
            RequireOpen(token);
            if (Remaining(value) <= TimeSpan.Zero) throw new OctoberSourceWindowException(OctoberSourceWindowFailure.Expired);
            if (result.Status != PreparedSourceAuthorityStatus.Current || result.CurrentApproval != value.Approval)
                throw new OctoberSourceWindowException(OctoberSourceWindowFailure.AuthorityUnavailable);
        }
        public async Task<TimeSpan> EnsureFileWindowAsync(TimeSpan requiredLifetime, CancellationToken cancellationToken = default)
        {
            if (requiredLifetime <= TimeSpan.Zero || requiredLifetime > TimeSpan.FromMinutes(30))
                throw new ArgumentOutOfRangeException(nameof(requiredLifetime));
            await _gate.WaitAsync(cancellationToken);
            try
            {
                RequireOpen(cancellationToken);
                requiredLifetime = Min(requiredLifetime, _grantWindow);
                if (requiredLifetime <= TimeSpan.Zero)
                    throw new OctoberSourceWindowException(OctoberSourceWindowFailure.InsufficientLifetime);
                _renewing = true;
                foreach (Guid repository in runtimes.Keys.ToArray())
                {
                    RuntimeBinding previous = runtimes[repository];
                    if (Remaining(previous) <= TimeSpan.Zero) throw new OctoberSourceWindowException(OctoberSourceWindowFailure.Expired);
                    // If the original run/session is limiting, issuing again cannot
                    // provide more time. Continue only within the existing window.
                    if (Remaining(previous) > Min(requiredLifetime, runRemaining()) ||
                        previous.Expires - owner._clock.GetUtcNow() >= runRemaining()) continue;
                    await VerifyOldAsync(previous, cancellationToken);
                    try { await previous.Runtime.DisposeAsync(); }
                    catch { throw new OctoberSourceWindowException(OctoberSourceWindowFailure.CleanupUnconfirmed); }
                    await VerifyOldAsync(previous, cancellationToken);
                    RuntimeBinding replacement = await owner.IssueRuntimeAsync(execution, previous.Selection, previous.Budget, previous.GraphOwner,
                        runRemaining, requiredLifetime, token => VerifyOldAsync(previous, token), cancellationToken);
                    try
                    {
                        RequireOpen(cancellationToken);
                        if (replacement.Approval.ApprovalIdentity == previous.Approval.ApprovalIdentity)
                            throw new OctoberSourceWindowException(OctoberSourceWindowFailure.BindingMismatch);
                        lock (runtimes) runtimes[repository] = replacement;
                    }
                    catch { await replacement.Runtime.DisposeAsync(); throw; }
                }
                RequireOpen(cancellationToken);
                // Later repository renewal can consume an earlier one's margin.
                // Recheck the complete set before handing any file to a Worker.
                TimeSpan remaining = runtimes.Values.Min(Remaining);
                if (remaining < Min(requiredLifetime, runRemaining()))
                    throw new OctoberSourceWindowException(OctoberSourceWindowFailure.InsufficientLifetime);
                TimeSpan usable = remaining - TimeSpan.FromSeconds(30);
                if (usable <= TimeSpan.Zero)
                    throw new OctoberSourceWindowException(OctoberSourceWindowFailure.InsufficientLifetime);
                return usable;
            }
            catch { _faulted = true; throw; }
            finally { _renewing = false; _gate.Release(); }
        }
        public OctoberToolRuntime GetRuntime(Guid repositoryId)
        {
            lock (runtimes)
                return !_closed && !_faulted && !_renewing && runtimes.TryGetValue(repositoryId, out RuntimeBinding? value)
                    ? value.Runtime : throw new InvalidOperationException("The owned Repository runtime is unavailable.");
        }

        public async Task<PreparedSourceResult> ListSourceAsync(Guid repositoryId, PreparedSourceListRequest request,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try { RequireOpen(cancellationToken); return await GetRuntime(repositoryId).ListAsync(request, cancellationToken); }
            finally { _gate.Release(); }
        }

        public async Task<OctoberSourceFrameResult> AcquireFrameAsync(Guid repositoryId, string relativePath,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try { RequireOpen(cancellationToken); return await GetRuntime(repositoryId).AcquireFileFrameAsync(relativePath, cancellationToken: cancellationToken); }
            finally { _gate.Release(); }
        }

        public ValueTask DisposeAsync()
        {
            lock (runtimes)
            {
                _closed = true;
                return new(_disposal ??= DisposeCoreAsync());
            }
        }
        private async Task DisposeCoreAsync()
        {
            await _gate.WaitAsync();
            try { await DisposeResourcesAsync(runtimes.Values); }
            finally { _gate.Release(); }
        }
    }
}
