using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Web;

/// <summary>
/// Combines an already issued source context with optional, separately authorized
/// navigation. The trusted caller retains ownership of the source context.
/// </summary>
public sealed class PreparedJavaWorkerToolComposer
{
    private readonly PreparedToolWorkspaceIssuer? _workspaceIssuer;
    private readonly PreparedJavaNavigationContextFactory? _navigationFactory;

    public PreparedJavaWorkerToolComposer(
        PreparedToolWorkspaceIssuer? workspaceIssuer,
        PreparedJavaNavigationContextFactory? navigationFactory)
    {
        _workspaceIssuer = workspaceIssuer;
        _navigationFactory = navigationFactory;
    }

    public async Task<PreparedJavaWorkerToolComposition> ComposeAsync(
        IPreparedSourceReadContext alreadyIssuedSourceContext,
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        PreparedToolWorkspaceGrantRequest applicationSelectedJdtGrant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alreadyIssuedSourceContext);
        IReadOnlyList<AIFunction> sourceFunctions =
            new PreparedSourceFunctions(alreadyIssuedSourceContext).Create();

        PreparedJavaWorkerToolComposition FileOnly(PreparedJavaNavigationOpenStatus status) =>
            new(status, sourceFunctions, navigationContext: null, workspaceLease: null);

        if (!MatchingTrustedGrants(
                alreadyIssuedSourceContext,
                applicationHeldSourceGrant,
                applicationSelectedJdtGrant))
        {
            return FileOnly(PreparedJavaNavigationOpenStatus.Denied);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return FileOnly(PreparedJavaNavigationOpenStatus.Cancelled);
        }

        if (_workspaceIssuer is null || _navigationFactory is null)
        {
            return FileOnly(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
        }

        PreparedToolWorkspaceIssueResult issue = await _workspaceIssuer.IssueAsync(
            applicationSelectedJdtGrant, cancellationToken);
        if (issue.Status != PreparedToolWorkspaceIssueStatus.Issued || issue.Lease is null)
        {
            return FileOnly(MapIssueStatus(issue.Status));
        }

        PreparedToolWorkspaceLease? lease = issue.Lease;
        IPreparedJavaNavigationContext? navigationContext = null;
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return FileOnly(PreparedJavaNavigationOpenStatus.Cancelled);
            }

            PreparedJavaNavigationOpenResult opened = await _navigationFactory.OpenAsync(
                lease, cancellationToken);
            navigationContext = opened.Context;
            if (opened.Status != PreparedJavaNavigationOpenStatus.Opened ||
                navigationContext is null)
            {
                return FileOnly(opened.Status == PreparedJavaNavigationOpenStatus.Opened
                    ? PreparedJavaNavigationOpenStatus.CleanupUnconfirmed
                    : opened.Status);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return FileOnly(PreparedJavaNavigationOpenStatus.Cancelled);
            }

            var composition = new PreparedJavaWorkerToolComposition(
                PreparedJavaNavigationOpenStatus.Opened,
                sourceFunctions,
                navigationContext,
                lease);
            navigationContext = null;
            lease = null;
            return composition;
        }
        finally
        {
            try
            {
                if (navigationContext is not null)
                {
                    await navigationContext.DisposeAsync();
                }
            }
            finally
            {
                if (lease is not null)
                {
                    await lease.DisposeAsync();
                }
            }
        }
    }

    private static bool MatchingTrustedGrants(
        IPreparedSourceReadContext readContext,
        PreparedSourceGrantRequest? sourceGrant,
        PreparedToolWorkspaceGrantRequest? jdtGrant) =>
        sourceGrant?.Approval is { } approval &&
        jdtGrant?.Approval == approval &&
        readContext.Lifetime == PreparedSourceContextLifetime.Open &&
        readContext.RepositoryId == approval.RepositoryId &&
        approval.Purpose == PreparedSourcePurpose.WorkerSourceRead &&
        jdtGrant.ToolPurpose == PreparedToolPurpose.JdtNavigation &&
        !string.IsNullOrWhiteSpace(sourceGrant.PhysicalRootPath) &&
        string.Equals(
            sourceGrant.PhysicalRootPath,
            jdtGrant.PhysicalRootPath,
            StringComparison.Ordinal);

    private static PreparedJavaNavigationOpenStatus MapIssueStatus(
        PreparedToolWorkspaceIssueStatus status) => status switch
        {
            PreparedToolWorkspaceIssueStatus.Denied => PreparedJavaNavigationOpenStatus.Denied,
            PreparedToolWorkspaceIssueStatus.AuthorityUnavailable =>
                PreparedJavaNavigationOpenStatus.AuthorityUnavailable,
            PreparedToolWorkspaceIssueStatus.RootUnavailable =>
                PreparedJavaNavigationOpenStatus.RootUnavailable,
            PreparedToolWorkspaceIssueStatus.Cancelled => PreparedJavaNavigationOpenStatus.Cancelled,
            PreparedToolWorkspaceIssueStatus.DeadlineExceeded =>
                PreparedJavaNavigationOpenStatus.DeadlineExceeded,
            _ => PreparedJavaNavigationOpenStatus.ConfigurationUnavailable,
        };
}

/// <summary>
/// Owns only the optional navigation context and its lease. Dispose after Worker
/// calls settle; the caller disposes its borrowed source context separately.
/// </summary>
public sealed class PreparedJavaWorkerToolComposition : IAsyncDisposable
{
    private readonly IPreparedJavaNavigationContext? _navigationContext;
    private readonly PreparedToolWorkspaceLease? _workspaceLease;
    private int _disposed;

    internal PreparedJavaWorkerToolComposition(
        PreparedJavaNavigationOpenStatus navigationStatus,
        IReadOnlyList<AIFunction> sourceFunctions,
        IPreparedJavaNavigationContext? navigationContext,
        PreparedToolWorkspaceLease? workspaceLease)
    {
        NavigationStatus = navigationStatus;
        SourceFunctions = sourceFunctions;
        _navigationContext = navigationContext;
        _workspaceLease = workspaceLease;
        NavigationFunctions = navigationContext is null
            ? []
            : new PreparedJavaNavigationFunctions(navigationContext).Create();
        WorkerFunctions = [.. SourceFunctions, .. NavigationFunctions];
    }

    public PreparedJavaNavigationOpenStatus NavigationStatus { get; }

    public IReadOnlyList<AIFunction> SourceFunctions { get; }

    public IReadOnlyList<AIFunction> NavigationFunctions { get; }

    public IReadOnlyList<AIFunction> WorkerFunctions { get; }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (_navigationContext is not null)
            {
                await _navigationContext.DisposeAsync();
            }
        }
        finally
        {
            if (_workspaceLease is not null)
            {
                await _workspaceLease.DisposeAsync();
            }
        }
    }
}
