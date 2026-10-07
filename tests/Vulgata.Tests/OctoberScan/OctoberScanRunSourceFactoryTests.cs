using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

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
        var selected = execution.Authority.SelectedContext!.Repositories.Single();
        return new([new(execution.Setup.TargetId, execution.Setup.BatchId, selected.RepositoryId, selected.RootBindingIdentity, Path.Combine(root, "source"))],
            Path.Combine(root, "tools", "codegraph.ps1"), new(Path.Combine(root, "state"), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
            new([Path.Combine(root, "tools")], [Path.Combine(root, "runtime")], Path.Combine(root, "system", "cmd.exe"), [Path.Combine(root, "tools")], Path.Combine(root, "jdk", "bin")), Path.Combine(root, "log"));
    }
    private sealed class Authority : IOctoberLocalAuthority
    {
        private readonly OctoberGenerateExecution _execution;
        internal Authority(OctoberGenerateExecution execution)
        {
            _execution = execution;
            var selected = execution.Authority.SelectedContext!.Repositories.Single();
            Approval = new(execution.Setup.BatchId, selected.RepositoryId, execution.Session.SubjectId, PreparedSourcePurpose.WorkerSourceRead, "controlled-source-factory-approval", selected.RootBindingIdentity, selected.OperatorLabel);
        }
        internal PreparedSourceApproval Approval { get; set; }
        internal bool Expired { get; set; }
        internal int Issues;
        public Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(IOctoberOperatorSession session, OctoberAuthoritySelectionRequest request, CancellationToken token) => Task.FromResult(new OctoberAuthoritySelectionResult(OctoberAuthorityDecisionStatus.Authorized, _execution.Setup.Snapshot.Request, _execution.Authority));
        public Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(IOctoberOperatorSession session, IOctoberHistoricalBoundSetupData setup, Guid repository, CancellationToken token)
        {
            Issues++; Assert.Same(_execution.Session, session);
            return Task.FromResult(new OctoberSourceApprovalResult(OctoberSourceApprovalStatus.Issued, Approval, Expired ? DateTimeOffset.UtcNow.AddSeconds(-1) : DateTimeOffset.UtcNow.AddMinutes(1)));
        }
        public Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(IOctoberOperatorSession session, string target, PreparedSourceApproval approval, CancellationToken token) => Task.FromResult(new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current, Approval));
        public Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(IOctoberOperatorSession session, string target, string identity, CancellationToken token) => Task.FromResult(OctoberRevocationStatus.Revoked);
    }
}
