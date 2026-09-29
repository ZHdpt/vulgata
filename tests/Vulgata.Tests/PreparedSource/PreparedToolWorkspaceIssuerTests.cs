using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedToolWorkspaceIssuerTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenLeaseDeadlinePasses_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(2)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(2_200));

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.DeadlineExceeded, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.ExpiredOrCancelled, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_AfterDisposal_ReturnsDisposedWithoutReopening()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        await lease.DisposeAsync();
        PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

        Assert.Equal(PreparedToolWorkspaceCheckStatus.Disposed, check);
        Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, lease.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenApprovalRootBindingChanges_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            fixture.Authority.Approve(fixture.Approval with
            {
                RootBindingIdentity = "replacement-root-binding",
            });

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenBothAuthoritiesAndRootRemainCurrent_LeavesLeaseOpen()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
            Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
            Assert.Equal(fixture.Approval, toolAuthorization.LastApproval);
            Assert.Equal(PreparedToolPurpose.JdtNavigation, toolAuthorization.LastPurpose);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenSourceVerifierBecomesUnavailable_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            fixture.Authority.MakeUnavailable();

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.AuthorityUnavailable, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task IssueAsync_WhenAlreadyCancelledOrExpired_ReturnsNoLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        PreparedToolWorkspaceIssueResult cancelled =
            await issuer.IssueAsync(request, cancellation.Token);
        PreparedToolWorkspaceIssueResult expired =
            await issuer.IssueAsync(request with { Deadline = TimeSpan.Zero });

        Assert.Equal(PreparedToolWorkspaceIssueStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.Lease);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.DeadlineExceeded, expired.Status);
        Assert.Null(expired.Lease);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenCancelled_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync(cancellation.Token);

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Cancelled, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.ExpiredOrCancelled, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Constructor_RequiresBothCurrentAuthorityVerifiers()
    {
        var sourceAuthority = new ControlledPreparedSourceAuthority();
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();

        Assert.Throws<ArgumentNullException>(
            () => new PreparedToolWorkspaceIssuer(null!, toolAuthorization));
        Assert.Throws<ArgumentNullException>(
            () => new PreparedToolWorkspaceIssuer(sourceAuthority, null!));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(
        PreparedToolUseAuthorityStatus.Denied,
        PreparedToolWorkspaceIssueStatus.Denied)]
    [InlineData(
        PreparedToolUseAuthorityStatus.Unavailable,
        PreparedToolWorkspaceIssueStatus.AuthorityUnavailable)]
    public async Task IssueAsync_WhenToolPurposeIsNotCurrent_ReturnsNoLease(
        PreparedToolUseAuthorityStatus toolStatus,
        PreparedToolWorkspaceIssueStatus expectedStatus)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        toolAuthorization.SetStatus(toolStatus);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult result = await issuer.IssueAsync(request);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Lease);
        Assert.Equal(fixture.Approval, toolAuthorization.LastApproval);
        Assert.Equal(PreparedToolPurpose.JdtNavigation, toolAuthorization.LastPurpose);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(
        PreparedToolUseAuthorityStatus.Denied,
        PreparedToolWorkspaceCheckStatus.Denied)]
    [InlineData(
        PreparedToolUseAuthorityStatus.Unavailable,
        PreparedToolWorkspaceCheckStatus.AuthorityUnavailable)]
    public async Task CheckCurrentAsync_WhenToolPurposeIsNoLongerCurrent_ClosesLease(
        PreparedToolUseAuthorityStatus toolStatus,
        PreparedToolWorkspaceCheckStatus expectedCheck)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            toolAuthorization.SetStatus(toolStatus);

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(expectedCheck, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
            Assert.Equal(fixture.Approval, toolAuthorization.LastApproval);
            Assert.Equal(PreparedToolPurpose.JdtNavigation, toolAuthorization.LastPurpose);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenSourceApprovalIsRevoked_ClosesLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            fixture.Authority.Revoke();

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, check);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckCurrentAsync_WhenPreparedRootIsReplaced_ClosesOriginalRootLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        try
        {
            string movedOriginalPath = Path.Combine(fixture.SandboxPath, "renamed-prepared-original");
            Directory.Move(fixture.PreparedRootPath, movedOriginalPath);
            Directory.CreateDirectory(fixture.PreparedRootPath);

            PreparedToolWorkspaceCheckStatus check = await lease.CheckCurrentAsync();

            Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, check);
            Assert.NotEqual(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task IssueAsync_CurrentExactApprovalAndJdtPurpose_ReturnsBoundTrustedLease()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer();
        toolAuthorization.Allow(fixture.Approval, PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(5));

        PreparedToolWorkspaceIssueResult result = await issuer.IssueAsync(request);

        if (result.Lease is not null)
        {
            await result.Lease.DisposeAsync();
        }

        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, result.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(result.Lease);
        Assert.Equal(fixture.Approval.RepositoryId, lease.RepositoryId);
        Assert.Equal(PreparedToolPurpose.JdtNavigation, lease.ToolPurpose);
        Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, lease.Lifetime);
        Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        Assert.Equal(fixture.Approval, toolAuthorization.LastApproval);
        Assert.Equal(PreparedToolPurpose.JdtNavigation, toolAuthorization.LastPurpose);

        string serializedLease = JsonSerializer.Serialize(lease);
        Assert.DoesNotContain(fixture.PreparedRootPath, serializedLease, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalRootPath", serializedLease, StringComparison.Ordinal);
    }

    private sealed class ControlledPreparedToolUseAuthorizer : IPreparedToolUseAuthorizer
    {
        private PreparedSourceApproval? _allowedApproval;
        private PreparedToolPurpose _allowedPurpose;
        private PreparedToolUseAuthorityStatus? _currentStatus;

        internal PreparedSourceApproval? LastApproval { get; private set; }

        internal PreparedToolPurpose? LastPurpose { get; private set; }

        internal void Allow(PreparedSourceApproval approval, PreparedToolPurpose purpose)
        {
            _allowedApproval = approval;
            _allowedPurpose = purpose;
            _currentStatus = PreparedToolUseAuthorityStatus.Current;
        }

        internal void SetStatus(PreparedToolUseAuthorityStatus status)
        {
            _currentStatus = status;
        }

        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastApproval = approval;
            LastPurpose = toolPurpose;
            PreparedToolUseAuthorityStatus status = approval == _allowedApproval &&
                toolPurpose == _allowedPurpose
                    ? _currentStatus ?? PreparedToolUseAuthorityStatus.Unavailable
                    : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }
}
