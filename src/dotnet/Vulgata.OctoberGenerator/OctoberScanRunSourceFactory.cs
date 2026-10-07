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
        var runtimes = new Dictionary<Guid, OctoberToolRuntime>();
        using var opening = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        opening.CancelAfter(remainingRunLifetime);
        try
        {
            foreach (OctoberRepositorySelection selected in execution.Setup.Snapshot.Request.Repositories)
            {
                opening.Token.ThrowIfCancellationRequested();
                Task<OctoberSourceApprovalResult> issuing = _authority.IssueSourceApprovalAsync(
                    execution.Session, execution.Setup, selected.RepositoryId, opening.Token);
                OctoberSourceApprovalResult issued;
                try { issued = await issuing.WaitAsync(opening.Token); }
                catch { _ = ObserveAsync(issuing); throw; }
                opening.Token.ThrowIfCancellationRequested();
                if (issued.Status != OctoberSourceApprovalStatus.Issued || issued.Approval is not { } approval ||
                    issued.ExpiresAtUtc is not { } expires || expires <= _clock.GetUtcNow() ||
                    approval.BatchId != execution.Setup.BatchId || approval.RepositoryId != selected.RepositoryId ||
                    approval.SubjectId != execution.Session.SubjectId || approval.Purpose != PreparedSourcePurpose.WorkerSourceRead ||
                    approval.RootBindingIdentity != selected.RootBindingIdentity || approval.OperatorLabel != selected.OperatorLabel ||
                    string.IsNullOrWhiteSpace(approval.ApprovalIdentity))
                    throw new InvalidOperationException("The exact live source approval is unavailable.");

                TimeSpan Remaining() => Min(remainingRunLifetime - _clock.GetElapsedTime(started),
                    Min(expires - _clock.GetUtcNow(), execution.Session.ExpiresAtUtc - _clock.GetUtcNow()));
                bool CurrentBinding(PreparedSourceApproval candidate) => candidate == approval && Remaining() > TimeSpan.Zero;
                var verifier = new OctoberPreparedSourceAuthorityVerifier(execution.Setup.TargetId,
                    async (target, candidate, token) =>
                    {
                        if (!CurrentBinding(candidate))
                            return new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
                        PreparedSourceAuthorityResult result = await _authority.VerifySourceApprovalAsync(
                            execution.Session, target, candidate, token);
                        token.ThrowIfCancellationRequested();
                        return CurrentBinding(candidate) ? result :
                            new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
                    }, CurrentBinding);
                var binding = new PreparedSourceBinding(new PreparedSourceContextIssuer(verifier),
                    execution.Setup.TargetId, _configuration.PreparedRoots);
                var workspace = new PreparedToolWorkspaceIssuer(verifier, new ToolAuthorization(approval, verifier));
                var java = new PreparedJavaNavigationContextFactory(_configuration.JavaNavigation, _configuration.JavaHostPolicy);
                var graph = new PreparedCodeGraphConnectionFactory(_configuration.CodeGraphCommandPath);
                TimeSpan remaining = Remaining();
                if (remaining <= TimeSpan.Zero) throw new InvalidOperationException("Source lifetime expired.");
                OctoberToolRuntime runtime = _createRuntime(approval, binding, workspace, java, graph, remaining);
                runtimes.Add(selected.RepositoryId, runtime);
                if (!runtime.HasPreparedBinding(selected.RepositoryId, approval.RootBindingIdentity, approval.ApprovalIdentity))
                    throw new InvalidOperationException("The owned runtime binding is unavailable.");
            }
            opening.Token.ThrowIfCancellationRequested();
            return new Source(runtimes);
        }
        catch
        {
            await DisposeRuntimesAsync(runtimes.Values);
            throw;
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;

    private static async Task ObserveAsync(Task task)
    {
        try { await task; } catch (Exception) { }
    }

    private static async Task DisposeRuntimesAsync(IEnumerable<OctoberToolRuntime> runtimes)
    {
        bool failed = false;
        foreach (OctoberToolRuntime runtime in runtimes)
        {
            try { await runtime.DisposeAsync(); }
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

    private sealed class Source(Dictionary<Guid, OctoberToolRuntime> runtimes) : IOctoberScanRunSource
    {
        private Task? _disposal;
        public OctoberToolRuntime GetRuntime(Guid repositoryId)
            => _disposal is null && runtimes.TryGetValue(repositoryId, out OctoberToolRuntime? runtime)
                ? runtime : throw new InvalidOperationException("The owned Repository runtime is unavailable.");

        public Task<PreparedSourceResult> ListSourceAsync(Guid repositoryId, PreparedSourceListRequest request,
            CancellationToken cancellationToken = default) => GetRuntime(repositoryId).ListAsync(request, cancellationToken);

        public Task<OctoberSourceFrameResult> AcquireFrameAsync(Guid repositoryId, string relativePath,
            CancellationToken cancellationToken = default)
            => GetRuntime(repositoryId).AcquireFileFrameAsync(relativePath, cancellationToken: cancellationToken);

        public ValueTask DisposeAsync()
        {
            lock (runtimes) return new(_disposal ??= DisposeRuntimesAsync(runtimes.Values));
        }
    }
}
