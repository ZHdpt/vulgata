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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_WithStateRootInsidePreparedRoot_RejectsBeforeRuntime()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string overlappingStateRoot = Path.Combine(fixture.PreparedRootPath, "jdt-state");
        Directory.CreateDirectory(overlappingStateRoot);
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

        var profile = new PreparedJavaNavigationProfile(
            overlappingStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new ControlledPreparedJdtLsRuntime();
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;
        try
        {
            open = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable, open.Status);
            Assert.Null(open.Context);
            Assert.Equal(0, runtime.OpenCallCount);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_WhenAuthorityIsRevokedAfterSessionReturn_DisposesSessionAndReleasesSlot()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult firstIssue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.JdtNavigation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, firstIssue.Status);
        PreparedToolWorkspaceLease firstLease = Assert.IsType<PreparedToolWorkspaceLease>(firstIssue.Lease);

        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var runtime = new RevokingSourceOnceRuntime(fixture.Authority);
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? firstOpen = null;
        PreparedToolWorkspaceLease? retryLease = null;
        PreparedJavaNavigationOpenResult? retryOpen = null;

        try
        {
            firstOpen = await factory.OpenAsync(firstLease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Denied, firstOpen.Status);
            Assert.Null(firstOpen.Context);
            Assert.Equal(PreparedToolWorkspaceLifetime.AuthorityClosed, firstLease.Lifetime);
            Assert.Equal(1, runtime.OpenCallCount);
            Assert.Equal(1, Assert.Single(runtime.Sessions).DisposeCallCount);

            fixture.Authority.Approve(fixture.Approval);
            PreparedToolWorkspaceIssueResult retryIssue = await issuer.IssueAsync(
                new PreparedToolWorkspaceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    PreparedToolPurpose.JdtNavigation,
                    TimeSpan.FromSeconds(10)));
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, retryIssue.Status);
            retryLease = Assert.IsType<PreparedToolWorkspaceLease>(retryIssue.Lease);

            retryOpen = await factory.OpenAsync(retryLease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, retryOpen.Status);
            Assert.NotNull(retryOpen.Context);
            Assert.Equal(2, runtime.OpenCallCount);
        }
        finally
        {
            if (retryOpen?.Context is not null)
            {
                await retryOpen.Context.DisposeAsync();
            }

            if (retryLease is not null)
            {
                await retryLease.DisposeAsync();
            }

            if (firstOpen?.Context is not null)
            {
                await firstOpen.Context.DisposeAsync();
            }

            await firstLease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetDefinitionAsync_WithContainedRawFileLocation_ReturnsRepositoryRelativeLocation()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string javaDirectory = Path.Combine(fixture.PreparedRootPath, "src", "main", "java", "example");
        Directory.CreateDirectory(javaDirectory);
        await File.WriteAllTextAsync(Path.Combine(javaDirectory, "Caller.java"), "package example; class Caller {}\n");
        string targetFilePath = Path.Combine(javaDirectory, "Target.java");
        await File.WriteAllTextAsync(targetFilePath, "package example; class Target {}\n");

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
        runtime.Session.Reply = new PreparedJdtLsRawReply(
            [new PreparedJdtLsRawLocation(new Uri(targetFilePath), 4, 9, 4, 21)],
            LimitReached: false,
            CapabilityLimitation: null);
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? open = null;

        try
        {
            open = await factory.OpenAsync(lease);
            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, open.Status);
            IPreparedJavaNavigationContext context = Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(open.Context);

            PreparedJavaNavigationResult result = await context.GetDefinitionAsync(
                new PreparedJavaNavigationRequest("src/main/java/example/Caller.java", 1, 1, 5));

            Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
            Assert.Equal(PreparedJavaNavigationOperation.Definition, result.Operation);
            Assert.Equal(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            Assert.Equal(
                new PreparedJavaLocation(
                    new PreparedSourceLocation(fixture.Approval.RepositoryId, "src/main/java/example/Target.java"),
                    4,
                    9,
                    4,
                    21),
                Assert.Single(result.Locations));
            Assert.Empty(result.Limitations);
            Assert.Null(result.StoppingLimit);
            Assert.Equal(1, runtime.Session.NavigateCallCount);
        }
        finally
        {
            if (open?.Context is not null)
            {
                await open.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_AfterUnconfirmedCleanup_QuarantinesRepositoryWithoutRetryingRuntime()
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
        var runtime = new UnconfirmedCleanupRuntime();
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);

        try
        {
            PreparedJavaNavigationOpenResult first = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, first.Status);
            Assert.Null(first.Context);

            PreparedJavaNavigationOpenResult retry = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, retry.Status);
            Assert.Null(retry.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_AfterConfirmedPreReturnCleanup_AllowsSameRepositoryRetry()
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
        var runtime = new ConfirmedCleanupThenSuccessRuntime();
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? retry = null;

        try
        {
            PreparedJavaNavigationOpenResult failedOpen = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.ReadinessFailed, failedOpen.Status);
            Assert.Null(failedOpen.Context);
            Assert.Equal(PreparedToolWorkspaceLifetime.Open, lease.Lifetime);
            Assert.Equal(1, runtime.OpenCallCount);

            retry = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, retry.Status);
            Assert.NotNull(retry.Context);
            Assert.Equal(2, runtime.OpenCallCount);
            Assert.Single(runtime.Sessions);
        }
        finally
        {
            if (retry?.Context is not null)
            {
                await retry.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_AfterUnexpectedRuntimeException_QuarantinesRepository()
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
        var runtime = new UnexpectedExceptionRuntime();
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        PreparedJavaNavigationOpenResult? retry = null;

        try
        {
            PreparedJavaNavigationOpenResult first = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, first.Status);
            Assert.Null(first.Context);

            retry = await factory.OpenAsync(lease);

            Assert.Equal(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, retry.Status);
            Assert.Null(retry.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            if (retry?.Context is not null)
            {
                await retry.Context.DisposeAsync();
            }

            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_WhenSameRepositorySlotIsBusy_CancelledWaitDoesNotInvokeRuntimeAgain()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var toolAuthorization = new ControlledPreparedToolUseAuthorizer(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        var request = new PreparedToolWorkspaceGrantRequest(
            fixture.Approval,
            fixture.PreparedRootPath,
            PreparedToolPurpose.JdtNavigation,
            TimeSpan.FromSeconds(10));
        PreparedToolWorkspaceIssueResult firstIssue = await issuer.IssueAsync(request);
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, firstIssue.Status);
        PreparedToolWorkspaceLease firstLease = Assert.IsType<PreparedToolWorkspaceLease>(firstIssue.Lease);

        PreparedToolWorkspaceLease? secondLease = null;
        Task<PreparedJavaNavigationOpenResult>? firstOpenTask = null;
        Task<PreparedJavaNavigationOpenResult>? secondOpenTask = null;
        PreparedJavaNavigationOpenResult? firstOpen = null;
        PreparedJavaNavigationOpenResult? secondOpen = null;
        var runtime = new ReservedSlotRuntime();
        string disposableStateRoot = Path.Combine(fixture.SiblingPath, "jdt-state");
        Directory.CreateDirectory(disposableStateRoot);
        var profile = new PreparedJavaNavigationProfile(
            disposableStateRoot,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            MaximumResponseBytes: 64 * 1024);
        var factory = new PreparedJavaNavigationContextFactory(profile, runtime);
        using var waitingCancellation = new CancellationTokenSource();

        try
        {
            PreparedToolWorkspaceIssueResult secondIssue = await issuer.IssueAsync(request);
            Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, secondIssue.Status);
            secondLease = Assert.IsType<PreparedToolWorkspaceLease>(secondIssue.Lease);
            Assert.Equal(firstLease.RepositoryId, secondLease.RepositoryId);

            firstOpenTask = factory.OpenAsync(firstLease);
            await runtime.FirstOpenStarted.WaitAsync(TimeSpan.FromSeconds(5));

            secondOpenTask = factory.OpenAsync(secondLease, waitingCancellation.Token);
            waitingCancellation.Cancel();
            secondOpen = await secondOpenTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOpenStatus.Cancelled, secondOpen.Status);
            Assert.Null(secondOpen.Context);
            Assert.Equal(1, runtime.OpenCallCount);

            runtime.ReleaseFirstOpen.TrySetResult(true);
            firstOpen = await firstOpenTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedJavaNavigationOpenStatus.Opened, firstOpen.Status);
            Assert.IsAssignableFrom<IPreparedJavaNavigationContext>(firstOpen.Context);
            Assert.Equal(1, runtime.OpenCallCount);
        }
        finally
        {
            runtime.ReleaseFirstOpen.TrySetResult(true);
            if (firstOpenTask is not null)
            {
                firstOpen ??= await firstOpenTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (secondOpenTask is not null)
            {
                secondOpen ??= await secondOpenTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            if (secondOpen?.Context is not null)
            {
                await secondOpen.Context.DisposeAsync();
            }

            if (firstOpen?.Context is not null)
            {
                await firstOpen.Context.DisposeAsync();
            }

            if (secondLease is not null)
            {
                await secondLease.DisposeAsync();
            }

            await firstLease.DisposeAsync();
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

    private sealed class RevokingSourceOnceRuntime(
        ControlledPreparedSourceAuthority sourceAuthority) : IPreparedJdtLsRuntime
    {
        public List<RecordingPreparedJdtLsSession> Sessions { get; } = [];

        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            if (OpenCallCount == 1)
            {
                sourceAuthority.Revoke();
            }

            var session = new RecordingPreparedJdtLsSession();
            Sessions.Add(session);
            return Task.FromResult<IPreparedJdtLsSession>(session);
        }
    }

    private sealed class UnconfirmedCleanupRuntime : IPreparedJdtLsRuntime
    {
        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            if (OpenCallCount == 1)
            {
                throw new PreparedJdtLsOpenFailureException(
                    PreparedJavaNavigationOpenStatus.ReadinessFailed,
                    cleanupConfirmed: false);
            }

            throw new InvalidOperationException("Unexpected runtime retry after unconfirmed cleanup.");
        }
    }

    private sealed class ConfirmedCleanupThenSuccessRuntime : IPreparedJdtLsRuntime
    {
        public int OpenCallCount { get; private set; }

        public List<RecordingPreparedJdtLsSession> Sessions { get; } = [];

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            if (OpenCallCount == 1)
            {
                throw new PreparedJdtLsOpenFailureException(
                    PreparedJavaNavigationOpenStatus.ReadinessFailed,
                    cleanupConfirmed: true);
            }

            var session = new RecordingPreparedJdtLsSession();
            Sessions.Add(session);
            return Task.FromResult<IPreparedJdtLsSession>(session);
        }
    }

    private sealed class UnexpectedExceptionRuntime : IPreparedJdtLsRuntime
    {
        public int OpenCallCount { get; private set; }

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCallCount++;
            if (OpenCallCount == 1)
            {
                throw new InvalidOperationException("Controlled runtime open failed without cleanup attestation.");
            }

            return Task.FromResult<IPreparedJdtLsSession>(new RecordingPreparedJdtLsSession());
        }
    }

    private sealed class ReservedSlotRuntime : IPreparedJdtLsRuntime
    {
        private readonly TaskCompletionSource<bool> _firstOpenStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _openCallCount;

        public Task FirstOpenStarted => _firstOpenStarted.Task;

        public TaskCompletionSource<bool> ReleaseFirstOpen { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int OpenCallCount => Volatile.Read(ref _openCallCount);

        public Task<IPreparedJdtLsSession> OpenAsync(
            PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile,
            CancellationToken cancellationToken = default)
        {
            int callNumber = Interlocked.Increment(ref _openCallCount);
            if (callNumber == 1)
            {
                _firstOpenStarted.TrySetResult(true);
                return ReturnFirstSessionAfterReleaseAsync(cancellationToken);
            }

            return Task.FromResult<IPreparedJdtLsSession>(new RecordingPreparedJdtLsSession());
        }

        private async Task<IPreparedJdtLsSession> ReturnFirstSessionAfterReleaseAsync(
            CancellationToken cancellationToken)
        {
            await ReleaseFirstOpen.Task.WaitAsync(cancellationToken);
            return new RecordingPreparedJdtLsSession();
        }
    }

    private sealed class RecordingPreparedJdtLsSession : IPreparedJdtLsSession
    {
        public PreparedJdtLsRawReply Reply { get; set; } = new([], false, null);

        public int NavigateCallCount { get; private set; }

        public PreparedJavaNavigationOperation? LastOperation { get; private set; }

        public PreparedJavaNavigationRequest? LastRequest { get; private set; }

        public int DisposeCallCount { get; private set; }

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NavigateCallCount++;
            LastOperation = operation;
            LastRequest = request;
            return Task.FromResult(Reply);
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
