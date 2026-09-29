using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;

using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.AnalysisRuntime;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class JdtLsPreparedNavigationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_WithCurrentJdtLease_PassesFixedProfileAndReturnsOneContext()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        bool contextDisposed = false;

        try
        {
            open = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context =
                Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Same(lease, runtime.LastWorkspaceLease);
            Assert.Equal(profile, runtime.LastProfile);
            Assert.Equal(CancellationToken.None, runtime.LastCancellationToken);
            Assert.Equal(PreparedToolPurpose.JdtNavigation, lease.ToolPurpose);

            await context.DisposeAsync();
            contextDisposed = true;

            Assert.Equal(1, runtime.Session.DisposeCallCount);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
        }
        finally
        {
            if (!contextDisposed && open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    private sealed class ControlledPreparedJdtLsRuntime : IPreparedJdtLsRuntime
    {
        public RecordingPreparedJdtLsSession Session { get; } = new();

        public int OpenCallCount { get; private set; }

        public PreparedToolWorkspaceLease? LastWorkspaceLease { get; private set; }

        public PreparedJavaNavigationProfile? LastProfile { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            LastWorkspaceLease = workspaceLease;
            LastProfile = profile;
            LastCancellationToken = cancellationToken;
            return Task.FromResult<IPreparedJdtLsSession>(Session);
        }
    }

    private sealed class RecordingPreparedJdtLsSession : IPreparedJdtLsSession
    {
        public int DisposeCallCount { get; private set; }

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedJdtLsRawReply([], false, null));
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ControlledPreparedToolUseAuthorizer(
        PreparedSourceApproval currentApproval,
        PreparedToolPurpose currentPurpose) : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedToolUseAuthorityStatus status = approval == currentApproval &&
                toolPurpose == currentPurpose
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }
}
