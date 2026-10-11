using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;
using System.Reflection;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanRunSourceFactoryTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExactSelection_ObtainsLiveApprovalAndBuildsBorrowedRuntimeWithinOriginalLifetimeAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var authority = new Authority(harness.Execution);
        int runtimes = 0;
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority, (approval, _, _, _, _, remaining) =>
        {
            runtimes++;
            Assert.Equal(authority.Approval, approval);
            Assert.InRange(remaining, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(30));
            return Inert(approval, remaining);
        });
        Assert.True(factory.ValidateSelection(harness.Execution));
        Assert.Equal(0, authority.Issues);
        await using var source = await factory.OpenAsync(harness.Execution, TimeSpan.FromSeconds(30));
        Assert.Equal(1, authority.Issues);
        Assert.Equal(1, runtimes);
        Assert.Same(source.GetRuntime(authority.Approval.RepositoryId), source.GetRuntime(authority.Approval.RepositoryId));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAsync_FourHourRunRenewsShortApprovalsAfterOneHourAndStopsOnRevocationAsync(bool revokeAfterOneHour)
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clock = new ControlledClock();
        DateTimeOffset runStartedAt = clock.GetUtcNow();
        var session = new LongSession("owned-operator", "controlled-authority", runStartedAt.AddHours(5));
        var execution = harness.Execution with
        {
            Session = session,
            Policy = harness.Execution.Policy with { RunDeadlineSeconds = 14400 }
        };
        var authority = new Authority(execution, clock) { ApprovalLifetime = TimeSpan.FromMinutes(10) };
        var runtimes = new List<OctoberToolRuntime>();
        var factory = new OctoberScanRunSourceFactory(Configuration(execution), authority,
            (approval, _, _, _, _, remaining) =>
            {
                var runtime = Inert(approval, remaining);
                runtimes.Add(runtime);
                return runtime;
            }, clock);
        await using var source = await factory.OpenAsync(execution, TimeSpan.FromHours(4));

        TimeSpan usableWindow = TimeSpan.Zero;
        for (int renewal = 0; renewal < 8; renewal++)
        {
            clock.Advance(TimeSpan.FromMinutes(8));
            if (revokeAfterOneHour && renewal == 7)
            {
                authority.VerifyStatus = PreparedSourceAuthorityStatus.Revoked;
                var error = await Record.ExceptionAsync(() => source.EnsureFileWindowAsync(TimeSpan.FromMinutes(5)));
                Assert.Equal(OctoberSourceWindowFailure.AuthorityUnavailable,
                    Assert.IsType<OctoberSourceWindowException>(error).Reason);
                Assert.True(clock.GetUtcNow() - runStartedAt > TimeSpan.FromHours(1));
                Assert.Equal(8, authority.Issues);
                Assert.Equal(8, runtimes.Count);
                return;
            }

            usableWindow = await source.EnsureFileWindowAsync(TimeSpan.FromMinutes(5));
        }

        Assert.True(clock.GetUtcNow() - runStartedAt > TimeSpan.FromHours(1));
        Assert.True(usableWindow > TimeSpan.FromMinutes(9));
        Assert.Equal(9, authority.Issues);
        Assert.Equal(9, runtimes.Count);
        Assert.All(authority.Verified, verified => Assert.Same(session, verified));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_FourHourRunStopsWhenOriginalAuthoritySessionExpiresAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clock = new ControlledClock();
        DateTimeOffset started = clock.GetUtcNow();
        var session = new LongSession("owned-operator", "controlled-authority", started.AddHours(3));
        var execution = harness.Execution with
        {
            Session = session,
            Policy = harness.Execution.Policy with { RunDeadlineSeconds = 14400 }
        };
        var authority = new Authority(execution, clock) { ApprovalLifetime = TimeSpan.FromMinutes(10) };
        var factory = new OctoberScanRunSourceFactory(Configuration(execution), authority,
            (approval, _, _, _, _, remaining) => Inert(approval, remaining), clock);
        await using var source = await factory.OpenAsync(execution, TimeSpan.FromHours(4));

        OctoberSourceWindowException? failure = null;
        for (int renewal = 0; renewal < 30 && failure is null; renewal++)
        {
            clock.Advance(TimeSpan.FromMinutes(8));
            try { await source.EnsureFileWindowAsync(TimeSpan.FromMinutes(5)); }
            catch (OctoberSourceWindowException error) { failure = error; }
        }

        Assert.NotNull(failure);
        Assert.Equal(OctoberSourceWindowFailure.Expired, failure.Reason);
        Assert.True(clock.GetUtcNow() - started >= TimeSpan.FromHours(3));
        Assert.True(clock.GetUtcNow() - started < TimeSpan.FromHours(4));
        Assert.Equal(23, authority.Issues);
        Assert.All(authority.Verified, verified => Assert.Same(session, verified));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task EnsureFileWindow_DefaultTenMinuteFileAndGrant_ReturnsUsableWindowAfterReserveAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clock = new ControlledClock();
        var authority = new Authority(harness.Execution, clock) { ApprovalLifetime = TimeSpan.FromSeconds(600) };
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority,
            (approval, _, _, _, _, remaining) => Inert(approval, remaining), clock);
        await using var source = await factory.OpenAsync(harness.Execution, TimeSpan.FromMinutes(30));

        TimeSpan usable = await source.EnsureFileWindowAsync(TimeSpan.FromSeconds(630));

        Assert.InRange(usable, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(570));
        Assert.Equal(1, authority.Issues);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task EnsureFileWindow_SequentialRepositoryRenewalsRecheckEarlierGrantBeforeReturningAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync(twoRepositories: true);
        var clock = new ControlledClock();
        var authority = new MultiRepositoryAuthority(harness.Execution, clock);
        int runtimeConstructions = 0;
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority,
            (approval, _, _, _, _, remaining) => { runtimeConstructions++; return Inert(approval, remaining); }, clock);
        await using var source = await factory.OpenAsync(harness.Execution, TimeSpan.FromMinutes(20));
        clock.Advance(TimeSpan.FromSeconds(35));

        var error = await Record.ExceptionAsync(() => source.EnsureFileWindowAsync(TimeSpan.FromSeconds(60)));

        Assert.Equal(OctoberSourceWindowFailure.InsufficientLifetime,
            Assert.IsType<OctoberSourceWindowException>(error).Reason);
        Assert.Equal(4, authority.Issues);
        Assert.Equal(4, runtimeConstructions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task EnsureFileWindow_RefreshesNearExpiryOnlyAfterCurrentApprovalAndCleanupAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clock = new ControlledClock();
        var authority = new Authority(harness.Execution, clock) { ApprovalLifetime = TimeSpan.FromMinutes(4) };
        var runtimes = new List<OctoberToolRuntime>();
        var runtimeApprovals = new List<PreparedSourceApproval>();
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority, (approval, _, _, _, _, remaining) =>
        {
            var runtime = Inert(approval, remaining);
            runtimes.Add(runtime);
            runtimeApprovals.Add(approval);
            return runtime;
        }, clock);
        await using var source = await factory.OpenAsync(harness.Execution, TimeSpan.FromMinutes(5));
        var first = source.GetRuntime(authority.Approval.RepositoryId);

        await source.EnsureFileWindowAsync(TimeSpan.FromMinutes(1));
        Assert.Equal(1, authority.Issues);
        Assert.Single(runtimes);

        clock.Advance(TimeSpan.FromMinutes(3));
        await source.EnsureFileWindowAsync(TimeSpan.FromMinutes(1));
        Assert.Equal(2, authority.Issues);
        Assert.Equal(2, runtimes.Count);
        Assert.NotSame(first, source.GetRuntime(authority.Approval.RepositoryId));
        Assert.NotEqual(runtimeApprovals[0].ApprovalIdentity, runtimeApprovals[1].ApprovalIdentity);
        Assert.True(runtimes[1].HasPreparedBinding(authority.Approval.RepositoryId,
            authority.Approval.RootBindingIdentity, runtimeApprovals[1].ApprovalIdentity));
        object firstOwner = Assert.IsAssignableFrom<object>(GraphOwner(runtimes[0]));
        Assert.Same(firstOwner, GraphOwner(runtimes[1]));
        Assert.All(authority.Verified, item => Assert.Same(harness.Execution.Session, item));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnsureFileWindow_ExpiredOrRevokedApprovalCannotBeRenewedAsync(bool expired)
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clock = new ControlledClock();
        var authority = new Authority(harness.Execution, clock) { ApprovalLifetime = TimeSpan.FromMinutes(4) };
        var runtimes = 0;
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority,
            (approval, _, _, _, _, remaining) => { runtimes++; return Inert(approval, remaining); }, clock);
        await using var source = await factory.OpenAsync(harness.Execution, TimeSpan.FromMinutes(5));
        if (expired) clock.Advance(TimeSpan.FromMinutes(5));
        else
        {
            clock.Advance(TimeSpan.FromMinutes(3));
            authority.VerifyStatus = PreparedSourceAuthorityStatus.Revoked;
        }

        var error = await Record.ExceptionAsync(() => source.EnsureFileWindowAsync(TimeSpan.FromMinutes(1)));
        Assert.Equal(expired ? OctoberSourceWindowFailure.Expired : OctoberSourceWindowFailure.AuthorityUnavailable,
            Assert.IsType<OctoberSourceWindowException>(error).Reason);
        Assert.Equal(1, authority.Issues);
        Assert.Equal(1, runtimes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task EnsureFileWindow_PostIssueRevocationCannotBeAdoptedAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clock = new ControlledClock();
        var authority = new Authority(harness.Execution, clock) { ApprovalLifetime = TimeSpan.FromMinutes(4), RevokeAfterSecondIssue = true };
        var runtimes = 0;
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority,
            (approval, _, _, _, _, remaining) => { runtimes++; return Inert(approval, remaining); }, clock);
        await using var source = await factory.OpenAsync(harness.Execution, TimeSpan.FromMinutes(5));
        clock.Advance(TimeSpan.FromMinutes(3));

        var error = await Record.ExceptionAsync(() => source.EnsureFileWindowAsync(TimeSpan.FromMinutes(1)));
        Assert.Equal(OctoberSourceWindowFailure.AuthorityUnavailable,
            Assert.IsType<OctoberSourceWindowException>(error).Reason);
        Assert.Equal(2, authority.Issues);
        Assert.Equal(1, runtimes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task EnsureFileWindow_ReplacementCarriesCumulativeCodeGraphSessionBudgetAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clock = new ControlledClock();
        var authority = new Authority(harness.Execution, clock) { ApprovalLifetime = TimeSpan.FromMinutes(4) };
        var runtimes = new List<OctoberToolRuntime>();
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority,
            (approval, _, _, _, _, remaining) =>
            {
                var runtime = Inert(approval, remaining);
                runtimes.Add(runtime);
                return runtime;
            }, clock);
        await using var source = await factory.OpenAsync(harness.Execution, TimeSpan.FromMinutes(5));
        object originalBudget = SessionBudget(runtimes[0]);
        for (int index = 0; index < 20; index++) Assert.True(TryReserve(originalBudget));

        clock.Advance(TimeSpan.FromMinutes(3));
        await source.EnsureFileWindowAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(2, runtimes.Count);
        object replacementBudget = SessionBudget(runtimes[1]);
        Assert.Same(originalBudget, replacementBudget);
        Assert.True(TryReserve(replacementBudget), "The one remaining reservation must survive approval-bound runtime replacement.");
        Assert.False(TryReserve(replacementBudget), "Replacing a runtime must not reset the 21-session repository limit.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task EnsureFileWindow_InsufficientFreshApprovalIsNotConstructedAsReplacementRuntimeAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clock = new ControlledClock();
        var authority = new Authority(harness.Execution, clock) { ApprovalLifetime = TimeSpan.FromMinutes(4) };
        var runtimes = 0;
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority,
            (approval, _, _, _, _, remaining) => { runtimes++; return Inert(approval, remaining); }, clock);
        await using var source = await factory.OpenAsync(harness.Execution, TimeSpan.FromMinutes(5));
        clock.Advance(TimeSpan.FromMinutes(3));
        authority.SubsequentApprovalLifetime = TimeSpan.FromSeconds(20);

        var error = await Record.ExceptionAsync(() => source.EnsureFileWindowAsync(TimeSpan.FromMinutes(1)));
        Assert.Equal(OctoberSourceWindowFailure.InsufficientLifetime,
            Assert.IsType<OctoberSourceWindowException>(error).Reason);
        Assert.Equal(2, authority.Issues);
        Assert.Equal(1, runtimes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ForeignConfiguredBatch_FailsSourceFreeBeforeApprovalOrRuntimeAsync()
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var authority = new Authority(harness.Execution);
        var configuration = Configuration(harness.Execution);
        configuration = configuration with { PreparedRoots = [configuration.PreparedRoots[0] with { BatchId = Guid.NewGuid() }] };
        int runtimes = 0;
        var factory = new OctoberScanRunSourceFactory(configuration, authority, (approval, _, _, _, _, remaining) => { runtimes++; return Inert(approval, remaining); });
        Assert.False(factory.ValidateSelection(harness.Execution));
        await Assert.ThrowsAnyAsync<Exception>(() => factory.OpenAsync(harness.Execution, TimeSpan.FromSeconds(30)));
        Assert.Equal(0, authority.Issues);
        Assert.Equal(0, runtimes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredOrForeignApproval_CannotBecomeARuntimeAsync(bool foreignRoot)
    {
        await using var harness = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var authority = new Authority(harness.Execution) { Expired = !foreignRoot };
        if (foreignRoot) authority.Approval = authority.Approval with { RootBindingIdentity = "foreign-root" };
        int runtimes = 0;
        var factory = new OctoberScanRunSourceFactory(Configuration(harness.Execution), authority, (approval, _, _, _, _, remaining) => { runtimes++; return Inert(approval, remaining); });
        await Assert.ThrowsAnyAsync<Exception>(() => factory.OpenAsync(harness.Execution, TimeSpan.FromSeconds(30)));
        Assert.Equal(1, authority.Issues);
        Assert.Equal(0, runtimes);
    }

    private static OctoberToolRuntime Inert(PreparedSourceApproval approval, TimeSpan remaining) => new(approval, remaining,
        new OctoberToolRuntimeDependencies((_, _) => throw new InvalidOperationException("No tool preparation during source opening"),
            (_, _, _) => throw new InvalidOperationException("No source read during source opening"),
            (_, _, _) => throw new InvalidOperationException("No identity read during source opening"),
            (_, _) => throw new InvalidOperationException("No native Java"), (_, _) => throw new InvalidOperationException("No native CodeGraph")));
    private static OctoberRuntimeConfiguration Configuration(OctoberGenerateExecution execution)
    {
        string root = Path.Combine(Path.GetTempPath(), "unopened-source-factory-test");
        var selected = execution.Authority.SelectedContext!.Repositories;
        return new([.. selected.Select(repository => new PreparedSourceRootBinding(execution.Setup.TargetId, execution.Setup.BatchId,
                repository.RepositoryId, repository.RootBindingIdentity, Path.Combine(root, repository.RepositoryId.ToString("N"))))],
            Path.Combine(root, "tools", "codegraph.ps1"), new(Path.Combine(root, "state"), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
            new([Path.Combine(root, "tools")], [Path.Combine(root, "runtime")], Path.Combine(root, "system", "cmd.exe"), [Path.Combine(root, "tools")], Path.Combine(root, "jdk", "bin")), Path.Combine(root, "log"));
    }

    private sealed class MultiRepositoryAuthority : IOctoberLocalAuthority
    {
        private readonly OctoberGenerateExecution _execution;
        private readonly ControlledClock _clock;
        private readonly OctoberRepositorySelection[] _selected;
        private readonly Dictionary<Guid, int> _perRepositoryIssues = [];
        private readonly Dictionary<Guid, PreparedSourceApproval> _approvals = [];
        internal MultiRepositoryAuthority(OctoberGenerateExecution execution, ControlledClock clock)
        {
            _execution = execution;
            _clock = clock;
            _selected = execution.Setup.Snapshot.Request.Repositories.ToArray();
            foreach (OctoberRepositorySelection repository in _selected)
            {
                _approvals.Add(repository.RepositoryId, new(execution.Setup.BatchId, repository.RepositoryId,
                    execution.Session.SubjectId, PreparedSourcePurpose.WorkerSourceRead,
                    $"controlled-multi-approval-{repository.RepositoryId:N}", repository.RootBindingIdentity, repository.OperatorLabel));
                _perRepositoryIssues.Add(repository.RepositoryId, 0);
            }
        }
        internal int Issues { get; private set; }
        public Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(IOctoberOperatorSession session,
            OctoberAuthoritySelectionRequest request, CancellationToken token) => Task.FromResult(
                new OctoberAuthoritySelectionResult(OctoberAuthorityDecisionStatus.Authorized,
                    _execution.Setup.Snapshot.Request, _execution.Authority));
        public Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(IOctoberOperatorSession session,
            IOctoberHistoricalBoundSetupData setup, Guid repository, CancellationToken token)
        {
            Assert.Same(_execution.Session, session);
            var selected = _selected.Single(item => item.RepositoryId == repository);
            int repoIssue = ++_perRepositoryIssues[repository];
            Issues++;
            var approval = _approvals[repository] with { ApprovalIdentity = $"multi-{repository:N}-{repoIssue}" };
            _approvals[repository] = approval;
            TimeSpan grant = repoIssue == 1
                ? repository == _selected[0].RepositoryId ? TimeSpan.FromSeconds(70) : TimeSpan.FromSeconds(75)
                : repository == _selected[0].RepositoryId ? TimeSpan.FromSeconds(45) : TimeSpan.FromSeconds(80);
            DateTimeOffset expiry = _clock.GetUtcNow().Add(grant);
            // The second sequential renewal consumes 10 seconds after issuance;
            // the prior repository's 45-second replacement then has only 35 seconds left.
            if (Issues == 4) _clock.Advance(TimeSpan.FromSeconds(10));
            return Task.FromResult(new OctoberSourceApprovalResult(OctoberSourceApprovalStatus.Issued, approval, expiry));
        }
        public Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(IOctoberOperatorSession session,
            string target, PreparedSourceApproval approval, CancellationToken token) => Task.FromResult(
                new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current, approval));
        public Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(IOctoberOperatorSession session,
            string target, string identity, CancellationToken token) => Task.FromResult(OctoberRevocationStatus.Revoked);
    }
    private sealed class Authority : IOctoberLocalAuthority
    {
        private readonly OctoberGenerateExecution _execution;
        private readonly TimeProvider _clock;
        internal Authority(OctoberGenerateExecution execution, TimeProvider? clock = null)
        {
            _execution = execution;
            _clock = clock ?? TimeProvider.System;
            var selected = execution.Authority.SelectedContext!.Repositories.Single();
            Approval = new(execution.Setup.BatchId, selected.RepositoryId, execution.Session.SubjectId, PreparedSourcePurpose.WorkerSourceRead, "controlled-source-factory-approval", selected.RootBindingIdentity, selected.OperatorLabel);
        }
        internal PreparedSourceApproval Approval { get; set; }
        internal bool Expired { get; set; }
        internal TimeSpan ApprovalLifetime { get; set; } = TimeSpan.FromMinutes(1);
        internal TimeSpan? SubsequentApprovalLifetime { get; set; }
        internal PreparedSourceAuthorityStatus VerifyStatus { get; set; } = PreparedSourceAuthorityStatus.Current;
        internal bool RevokeAfterSecondIssue { get; set; }
        internal List<IOctoberOperatorSession> Verified { get; } = [];
        internal int Issues;
        public Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(IOctoberOperatorSession session, OctoberAuthoritySelectionRequest request, CancellationToken token) => Task.FromResult(new OctoberAuthoritySelectionResult(OctoberAuthorityDecisionStatus.Authorized, _execution.Setup.Snapshot.Request, _execution.Authority));
        public Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(IOctoberOperatorSession session, IOctoberHistoricalBoundSetupData setup, Guid repository, CancellationToken token)
        {
            Issues++; Assert.Same(_execution.Session, session);
            Approval = Approval with { ApprovalIdentity = $"controlled-source-factory-approval-{Issues}" };
            if (Issues >= 2 && RevokeAfterSecondIssue) VerifyStatus = PreparedSourceAuthorityStatus.Revoked;
            return Task.FromResult(new OctoberSourceApprovalResult(OctoberSourceApprovalStatus.Issued, Approval,
                Expired ? _clock.GetUtcNow().AddSeconds(-1) : _clock.GetUtcNow().Add(Issues > 1 ? SubsequentApprovalLifetime ?? ApprovalLifetime : ApprovalLifetime)));
        }
        public Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(IOctoberOperatorSession session, string target, PreparedSourceApproval approval, CancellationToken token)
        {
            Verified.Add(session);
            var status = session.ExpiresAtUtc <= _clock.GetUtcNow()
                ? PreparedSourceAuthorityStatus.Revoked : VerifyStatus;
            return Task.FromResult(new PreparedSourceAuthorityResult(status,
                status == PreparedSourceAuthorityStatus.Current ? approval : null));
        }
        public Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(IOctoberOperatorSession session, string target, string identity, CancellationToken token) => Task.FromResult(OctoberRevocationStatus.Revoked);
    }

    private sealed class ControlledClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        private long _ticks;
        public override DateTimeOffset GetUtcNow() => _now;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Advance(TimeSpan duration) { _now += duration; _ticks += duration.Ticks; }
    }

    private sealed record LongSession(string SubjectId, string ManagementAuthorityId,
        DateTimeOffset ExpiresAtUtc) : IOctoberOperatorSession
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static object SessionBudget(OctoberToolRuntime runtime) =>
        typeof(OctoberToolRuntime).GetField("_sessionBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;

    private static bool TryReserve(object budget) =>
        (bool)budget.GetType().GetMethod("TryReserve", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(budget, null)!;

    private static object? GraphOwner(OctoberToolRuntime runtime) =>
        typeof(OctoberToolRuntime).GetField("_graphOwner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime);
}
