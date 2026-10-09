using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Source;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Controlled S1 runtime composition; factories are inert and all native/provider access is replaced.</summary>
public sealed class OctoberScanToolRuntimeTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_MultiplePages_UsesOneContextAndItsOwnCursorAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var page = 0;
        var source = harness.Context(request => ++page == 1
            ? Listed(harness.RepositoryId, ["a.cs"], "same-context") with
            {
                Entries = [new(new(harness.RepositoryId, "a.cs"), PreparedSourceEntryKind.NonTraversableLink)],
                Limitations = [new(new(harness.RepositoryId, "a.cs"), PreparedSourceLimitationReason.NonTraversableLink)],
            }
            : request.Continuation == "same-context" ? Listed(harness.RepositoryId, ["b.cs"]) : throw new InvalidOperationException("Lost continuation"));
        harness.Sources.Enqueue(source);
        await using var runtime = harness.CreateRuntime();
        var result = await runtime.ListAsync(new("", 1));
        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal(new[] { "a.cs", "b.cs" }, result.Entries.Select(entry => entry.Location.RelativePath));
        Assert.Equal(1, harness.SourceIssues);
        Assert.Equal(PreparedSourceEntryKind.NonTraversableLink, result.Entries[0].Kind);
        var limitation = Assert.Single(result.Limitations);
        Assert.Equal(PreparedSourceLimitationReason.NonTraversableLink, limitation.Reason);
        Assert.Equal("a.cs", limitation.Location.RelativePath);
        Assert.True(source.Disposed);
        Assert.Null(result.Continuation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_OneExpiredContext_RestartsWithFreshCursorAndDeduplicatesStableIdentityAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var firstCalls = 0;
        var first = harness.Context(_ => ++firstCalls == 1 ? Listed(harness.RepositoryId, ["a.cs"], "old-cursor") : Failed(harness.RepositoryId, PreparedSourceOutcome.DeadlineExceeded));
        var restarted = harness.Context(request =>
        {
            Assert.Null(request.Continuation);
            return Listed(harness.RepositoryId, ["a.cs", "b.cs"]);
        });
        harness.Sources.Enqueue(first);
        harness.Sources.Enqueue(restarted);
        await using var runtime = harness.CreateRuntime();
        var result = await runtime.ListAsync(new("", 1));
        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal(new[] { "a.cs", "b.cs" }, result.Entries.Select(entry => entry.Location.RelativePath));
        Assert.Equal(2, harness.SourceIssues);
        Assert.True(first.Disposed);
        Assert.True(restarted.Disposed);
        Assert.True(harness.IdentityObservations >= 3);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_RestartFindsChangedIdentity_FailsInsteadOfPathOnlyDedupAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var calls = 0;
        harness.Sources.Enqueue(harness.Context(_ => ++calls == 1 ? Listed(harness.RepositoryId, ["a.cs"], "old") : Failed(harness.RepositoryId, PreparedSourceOutcome.DeadlineExceeded)));
        var replacement = harness.Context(_ => Listed(harness.RepositoryId, ["a.cs", "b.cs"]));
        replacement.IdentitySuffix = "changed-object-or-content";
        harness.Sources.Enqueue(replacement);
        await using var runtime = harness.CreateRuntime();
        var result = await runtime.ListAsync(new("", 1));
        Assert.Equal(PreparedSourceOutcome.ChangedInput, result.Outcome);
        Assert.DoesNotContain(result.Entries, entry => entry.Location.RelativePath == "b.cs");
        Assert.Equal(2, harness.SourceIssues);
        var preparationsBeforeBlockedCall = harness.Preparations;
        var later = await runtime.ExploreAsync("symbol", 2);
        Assert.NotEqual(CodeGraphToolInvocationStatus.Complete, later.Status);
        Assert.Equal(preparationsBeforeBlockedCall, harness.Preparations);
        Assert.Empty(harness.CodeGraphSessions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_SecondContextExpires_StopsIncompleteWithoutThirdGrantAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var firstCalls = 0;
        var secondCalls = 0;
        harness.Sources.Enqueue(harness.Context(_ => ++firstCalls == 1 ? Listed(harness.RepositoryId, ["a.cs"], "first") : Failed(harness.RepositoryId, PreparedSourceOutcome.DeadlineExceeded)));
        harness.Sources.Enqueue(harness.Context(_ => ++secondCalls == 1 ? Listed(harness.RepositoryId, ["a.cs"], "second") : Failed(harness.RepositoryId, PreparedSourceOutcome.DeadlineExceeded)));
        await using var runtime = harness.CreateRuntime();
        var result = await runtime.ListAsync(new("", 1));
        Assert.Equal(PreparedSourceOutcome.Incomplete, result.Outcome);
        Assert.Equal(2, harness.SourceIssues);
        Assert.Null(result.Continuation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_CurrentAuthorityRevoked_ReturnsNoEarlierPageAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var calls = 0;
        harness.Sources.Enqueue(harness.Context(_ => ++calls == 1 ? Listed(harness.RepositoryId, ["a.cs"], "first") : Failed(harness.RepositoryId, PreparedSourceOutcome.Denied)));
        await using var runtime = harness.CreateRuntime();
        var result = await runtime.ListAsync(new("", 1));
        Assert.Equal(PreparedSourceOutcome.Denied, result.Outcome);
        Assert.Empty(result.Entries);
        Assert.Null(result.Continuation);
        Assert.Equal(1, harness.SourceIssues);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_TwoCallsExpiryThenJavaTransition_ReusesAndReplacesWithinSameDescriptorAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        await using var runtime = harness.CreateRuntime();
        Assert.Equal(CodeGraphToolInvocationStatus.Complete, (await runtime.ExploreAsync("symbol", 2)).Status);
        Assert.Equal(CodeGraphToolInvocationStatus.Complete, (await runtime.ExploreAsync("symbol", 2)).Status);
        Assert.Equal(1, harness.Preparations);
        var first = Assert.Single(harness.CodeGraphSessions);
        Assert.False(first.Disposed);
        first.Expired = true;
        Assert.Equal(CodeGraphToolInvocationStatus.Complete, (await runtime.ExploreAsync("symbol", 2)).Status);
        Assert.True(first.Disposed);
        Assert.Equal(2, harness.CodeGraphSessions.Count);
        Assert.Equal(PreparedJavaNavigationOutcome.Observed, (await runtime.NavigateAsync(PreparedJavaNavigationOperation.Definition, new("a.java", 1, 1, 10))).Outcome);
        Assert.True(first.Disposed);
        Assert.Equal(CodeGraphToolInvocationStatus.Complete, (await runtime.ExploreAsync("symbol", 2)).Status);
        Assert.Equal(1, harness.Preparations);
        Assert.Equal(3, harness.CodeGraphSessions.Count);
        Assert.True(harness.CodeGraphSessions[1].Disposed);
        Assert.False(harness.CodeGraphSessions[2].Disposed);
        await runtime.DisposeAsync();
        Assert.All(harness.CodeGraphSessions, session => Assert.True(session.Disposed));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_JavaPhaseInFlight_WaitsForVerifiedJavaDisposalAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Java.Started = started;
        harness.Java.Release = settled.Task;
        await using var runtime = harness.CreateRuntime();
        var navigation = runtime.NavigateAsync(PreparedJavaNavigationOperation.Definition, new("a.java", 1, 1, 10));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var explore = runtime.ExploreAsync("symbol", 2);
        try
        {
            Assert.False(explore.IsCompleted);
            Assert.Empty(harness.CodeGraphSessions);
        }
        finally
        {
            settled.TrySetResult();
            await navigation;
        }
        Assert.Equal(CodeGraphToolInvocationStatus.Complete, (await explore).Status);
        Assert.True(harness.Java.Disposed);
        Assert.True(harness.JavaWasDisposedBeforeCodeGraph);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_QueuedCancellation_StartsNoNativePhaseAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Java.Started = started;
        harness.Java.Release = release.Task;
        await using var runtime = harness.CreateRuntime();
        var navigation = runtime.NavigateAsync(PreparedJavaNavigationOperation.Definition, new("a.java", 1, 1, 10));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        var queued = runtime.ExploreAsync("symbol", 2, cancellation.Token);
        cancellation.Cancel();
        try
        {
            var result = await queued.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(CodeGraphToolInvocationStatus.Cancelled, result.Status);
            Assert.Empty(harness.CodeGraphSessions);
        }
        finally
        {
            release.TrySetResult();
            await navigation;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task NavigateAsync_UnconfirmedCleanup_QuarantinesSubsequentToolPhasesAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        harness.Java.FailCleanup = true;
        var runtime = harness.CreateRuntime();
        try
        {
            var navigation = await runtime.NavigateAsync(PreparedJavaNavigationOperation.Definition, new("a.java", 1, 1, 10));
            Assert.NotEqual(PreparedJavaNavigationOutcome.Observed, navigation.Outcome);
            var explore = await runtime.ExploreAsync("symbol", 2);
            Assert.NotEqual(CodeGraphToolInvocationStatus.Complete, explore.Status);
            Assert.Empty(harness.CodeGraphSessions);
        }
        finally
        {
            await Record.ExceptionAsync(() => runtime.DisposeAsync().AsTask());
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchTextAsync_MultipleMatchesInOneFileAndNextFile_PreservesOffsetsAcrossPagesAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var calls = 0;
        var context = harness.Context(_ => throw new InvalidOperationException("Search must not switch to listing"));
        context.Search = request =>
        {
            Assert.Equal("needle", request.Literal);
            Assert.Equal("src", request.RelativeDirectory);
            calls++;
            Assert.Equal(calls == 1 ? null : "page-" + (calls - 1), request.Continuation);
            var path = calls < 3 ? "src/a.cs" : "src/b.cs";
            return new PreparedSourceResult
            {
                RepositoryId = harness.RepositoryId, Operation = PreparedSourceOperation.SearchText,
                Outcome = calls < 3 ? PreparedSourceOutcome.Limited : PreparedSourceOutcome.Complete,
                Matches = [new(new(harness.RepositoryId, path, calls == 2 ? 12 : 0, 6, calls == 2 ? 2 : 1, 1), "needle")],
                Continuation = calls < 3 ? "page-" + calls : null,
                StoppingLimit = calls < 3 ? PreparedSourceStoppingLimit.PageSize : null,
            };
        };
        harness.Sources.Enqueue(context);
        await using var runtime = harness.CreateRuntime();
        var result = await runtime.SearchTextAsync(new("src", "needle", 1));
        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal(3, result.Matches.Count);
        Assert.Equal(new long[] { 0, 12 }, result.Matches.Where(match => match.Location.RelativePath == "src/a.cs").Select(match => match.Location.ByteStart));
        Assert.Single(result.Matches.Where(match => match.Location.RelativePath == "src/b.cs"));
        Assert.Equal(1, harness.SourceIssues);
        Assert.True(context.Disposed);
        Assert.Null(result.Continuation);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_IdentityUnavailableFromOpenBudgetBoundContext_ReportsIncompleteNotChangedInputAsync()
    {
        await using var harness = await RuntimeHarness.CreateAsync();
        var context = harness.Context(_ => Listed(harness.RepositoryId, ["a.cs"]));
        context.IdentityAvailable = false;
        harness.Sources.Enqueue(context);
        await using var runtime = harness.CreateRuntime();
        var result = await runtime.ListAsync(new("", 1));
        Assert.Equal(PreparedSourceOutcome.Incomplete, result.Outcome);
        Assert.Null(result.Continuation);
        Assert.InRange(harness.SourceIssues, 1, 2);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task NavigateAsync_PublicFactoryReportsUnconfirmedOpenCleanup_DisposalCannotClaimSuccessAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var binding = new PreparedSourceBinding(new PreparedSourceContextIssuer(fixture.Authority), "owned-target", [new("owned-target", fixture.Approval.BatchId, fixture.Approval.RepositoryId, fixture.Approval.RootBindingIdentity, fixture.PreparedRootPath)]);
        var workspace = new PreparedToolWorkspaceIssuer(fixture.Authority, new ControlledToolAuthorizer(fixture.Approval));
        var stateRoot = Path.Combine(fixture.SiblingPath, "unused-state");
        Directory.CreateDirectory(stateRoot);
        var profile = new PreparedJavaNavigationProfile(stateRoot, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), 1024);
        var unconfirmed = new UnconfirmedJavaRuntime();
        var java = new PreparedJavaNavigationContextFactory(profile, unconfirmed);
        var codeGraph = new PreparedCodeGraphConnectionFactory(Path.Combine(fixture.SandboxPath, "never-executed.ps1"));
        var runtime = new OctoberToolRuntime(fixture.Approval, binding, workspace, java, codeGraph, TimeSpan.FromMinutes(1));
        try
        {
            var result = await runtime.NavigateAsync(PreparedJavaNavigationOperation.Definition, new("a.java", 1, 1, 10));
            Assert.NotEqual(PreparedJavaNavigationOutcome.Observed, result.Outcome);
            Assert.Equal(1, unconfirmed.OpenCalls);
            var lease = Assert.Single(runtime.JdtLeases);
            Assert.False(lease.OwnedResourcesSettled);
            await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.DisposeAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.DisposeAsync().AsTask());
            Assert.Null(runtime.PreparedConnection);
        }
        finally
        {
            await Record.ExceptionAsync(() => runtime.DisposeAsync().AsTask());
            // The controlled runtime starts no process/resources; after assertions release its test-owned root handles.
            foreach (var lease in runtime.JdtLeases)
                await lease.DisposeAsync();
        }
    }

    // The public adapter must preserve positively settled ordinary JDT limitations.
    // A second explicit caller invocation proves usability, not an automatic retry.
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable)]
    [InlineData(PreparedJavaNavigationOpenStatus.ReadinessFailed)]
    public async Task ConfirmedOrdinaryJavaOpenFailure_PreservesLimitationAndRuntimeUsabilityAsync(PreparedJavaNavigationOpenStatus failure)
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        var binding = new PreparedSourceBinding(issuer, "owned-target",
            [new("owned-target", fixture.Approval.BatchId, fixture.Approval.RepositoryId,
                fixture.Approval.RootBindingIdentity, fixture.PreparedRootPath)]);
        var workspace = new PreparedToolWorkspaceIssuer(fixture.Authority, new ControlledToolAuthorizer(fixture.Approval));
        string stateRoot = Path.Combine(fixture.SiblingPath, "unused-confirmed-state");
        Directory.CreateDirectory(stateRoot);
        var profile = new PreparedJavaNavigationProfile(stateRoot, TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), 1024);
        var inert = new ConfirmedFailureJavaRuntime(failure);
        var java = new PreparedJavaNavigationContextFactory(profile, inert);
        var codeGraph = new PreparedCodeGraphConnectionFactory(Path.Combine(fixture.SandboxPath, "never-executed.ps1"));
        await using var runtime = new OctoberToolRuntime(fixture.Approval, binding, workspace, java, codeGraph, TimeSpan.FromMinutes(1));

        PreparedJavaNavigationResult navigation = await runtime.NavigateAsync(
            PreparedJavaNavigationOperation.Definition, new("a.java", 1, 1, 10));
        var expected = failure == PreparedJavaNavigationOpenStatus.ConfigurationUnavailable
            ? PreparedJavaNavigationOutcome.ConfigurationUnavailable : PreparedJavaNavigationOutcome.ReadinessFailed;
        Assert.Equal(expected, navigation.Outcome);
        Assert.Empty(navigation.Locations);
        PreparedJavaLimitation limitation = Assert.Single(navigation.Limitations);
        Assert.Equal(failure == PreparedJavaNavigationOpenStatus.ConfigurationUnavailable
            ? "jdt_configuration_unavailable" : "jdt_readiness_failed", limitation.Code);
        Assert.Null(limitation.Source);
        Assert.Equal(1, inert.OpenCalls);
        var lease = Assert.Single(runtime.JdtLeases);
        Assert.True(lease.OwnedResourcesSettled);
        Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, lease.Lifetime);

        var second = await runtime.NavigateAsync(PreparedJavaNavigationOperation.Definition, new("a.java", 1, 1, 10));
        Assert.Equal(expected, second.Outcome);
        Assert.Empty(second.Locations);
        Assert.Equal(2, inert.OpenCalls);
        Assert.Equal(2, runtime.JdtLeases.Count);
        Assert.All(runtime.JdtLeases, owned =>
        {
            Assert.True(owned.OwnedResourcesSettled);
            Assert.Equal(PreparedToolWorkspaceLifetime.Disposed, owned.Lifetime);
        });
        Assert.Null(runtime.PreparedConnection); // Neither explicit navigation attempt launches CodeGraph.

        PreparedSourceIssueResult independent = await issuer.IssueAsync(new(fixture.Approval,
            fixture.PreparedRootPath, PreparedSourceGrantLimits.Default));
        Assert.Equal(PreparedSourceIssueStatus.Issued, independent.Status);
        await using IPreparedSourceReadContext context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(independent.Context);
        Assert.Equal(PreparedSourceContextLifetime.Open, context.Lifetime);
        Assert.Equal(fixture.Approval.RepositoryId, context.RepositoryId);
        await runtime.DisposeAsync();
        Assert.Equal(2, inert.OpenCalls);
    }

    private sealed class ConfirmedFailureJavaRuntime(PreparedJavaNavigationOpenStatus status) : IPreparedJdtLsRuntime
    {
        internal int OpenCalls;
        public Task<IPreparedJdtLsSession> OpenAsync(PreparedToolWorkspaceLease workspaceLease,
            PreparedJavaNavigationProfile profile, CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            return Task.FromException<IPreparedJdtLsSession>(new PreparedJdtLsOpenFailureException(status, cleanupConfirmed: true));
        }
    }

    private sealed class UnconfirmedJavaRuntime : IPreparedJdtLsRuntime
    {
        internal int OpenCalls { get; private set; }
        public Task<IPreparedJdtLsSession> OpenAsync(PreparedToolWorkspaceLease workspaceLease, PreparedJavaNavigationProfile profile, CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            return Task.FromException<IPreparedJdtLsSession>(new PreparedJdtLsOpenFailureException(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed, cleanupConfirmed: false));
        }
    }

    private sealed class ControlledToolAuthorizer(PreparedSourceApproval expected) : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(PreparedSourceApproval approval, PreparedToolPurpose toolPurpose, CancellationToken cancellationToken = default)
            => Task.FromResult(new PreparedToolUseAuthorityResult(approval == expected ? PreparedToolUseAuthorityStatus.Current : PreparedToolUseAuthorityStatus.Denied));
    }
    private static PreparedSourceResult Listed(Guid repository, string[] paths, string? continuation = null) => new()
    {
        RepositoryId = repository, Operation = PreparedSourceOperation.List,
        Outcome = continuation is null ? PreparedSourceOutcome.Complete : PreparedSourceOutcome.Limited,
        Entries = paths.Select(path => new PreparedSourceEntry(new(repository, path), PreparedSourceEntryKind.RegularFile)).ToArray(),
        Continuation = continuation, StoppingLimit = continuation is null ? null : PreparedSourceStoppingLimit.PageSize,
    };
    private static PreparedSourceResult Failed(Guid repository, PreparedSourceOutcome outcome) => new() { RepositoryId = repository, Operation = PreparedSourceOperation.List, Outcome = outcome };

    internal sealed class RuntimeHarness : IAsyncDisposable
    {
        private readonly PreparedSourceFixture _fixture;
        private readonly PreparedCodeGraphConnection _descriptor;
        private RuntimeHarness(PreparedSourceFixture fixture)
        {
            _fixture = fixture;
            Java = new(fixture.Approval.RepositoryId);
            _descriptor = new(fixture.Approval.RepositoryId, Path.Combine(fixture.SandboxPath, "never-executed.ps1"), "never-executed", [], fixture.PreparedRootPath, new Dictionary<string, string>(), static () => { }, _ => NewCodeGraph());
        }
        internal PreparedSourceApproval Approval => _fixture.Approval;
        internal Guid RepositoryId => _fixture.Approval.RepositoryId;
        internal Queue<ScriptedSourceContext> Sources { get; } = new();
        internal int SourceIssues { get; private set; }
        internal int Preparations { get; private set; }
        internal int IdentityObservations { get; private set; }
        internal List<ControlledCodeGraph> CodeGraphSessions { get; } = [];
        internal ControlledJava Java { get; }
        internal bool JavaWasDisposedBeforeCodeGraph { get; private set; }
        internal static async Task<RuntimeHarness> CreateAsync()
        {
            var fixture = await PreparedSourceFixture.CreateAsync();
            Directory.CreateDirectory(Path.Combine(fixture.PreparedRootPath, ".codegraph"));
            return new(fixture);
        }
        internal ScriptedSourceContext Context(Func<PreparedSourceListRequest, PreparedSourceResult> list) => new(RepositoryId, list);
        internal OctoberToolRuntime CreateRuntime() => new(_fixture.Approval, TimeSpan.FromMinutes(5), new OctoberToolRuntimeDependencies(
            (_, token) => { token.ThrowIfCancellationRequested(); Preparations++; return Task.FromResult(_descriptor); },
            (_, _, token) =>
            {
                token.ThrowIfCancellationRequested();
                SourceIssues++;
                IPreparedSourceReadContext context = Sources.Count > 0 ? Sources.Dequeue() : Context(_ => Listed(RepositoryId, []));
                return Task.FromResult(new PreparedSourceIssueResult(PreparedSourceIssueStatus.Issued, context));
            },
            (context, path, token) => { token.ThrowIfCancellationRequested(); IdentityObservations++; return Task.FromResult<string?>(((ScriptedSourceContext)context).IdentityAvailable ? path + ":" + ((ScriptedSourceContext)context).IdentitySuffix : null); },
            (_, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult<IPreparedJavaNavigationContext>(Java); },
            (descriptor, _) => { Assert.Same(_descriptor, descriptor); return descriptor.OpenSession(); }));
        private ControlledCodeGraph NewCodeGraph()
        {
            JavaWasDisposedBeforeCodeGraph = Java.Disposed;
            var session = new ControlledCodeGraph(RepositoryId);
            CodeGraphSessions.Add(session);
            return session;
        }
        public async ValueTask DisposeAsync()
        {
            await _descriptor.DisposeAsync();
            await _fixture.DisposeAsync();
        }
    }

    internal sealed class ScriptedSourceContext(Guid repository, Func<PreparedSourceListRequest, PreparedSourceResult> list) : IPreparedSourceReadContext
    {
        public Guid RepositoryId => repository;
        public PreparedSourceContextLifetime Lifetime => Disposed ? PreparedSourceContextLifetime.Disposed : PreparedSourceContextLifetime.Open;
        internal bool Disposed { get; private set; }
        internal string IdentitySuffix { get; set; } = "stable";
        internal bool IdentityAvailable { get; set; } = true;
        internal Func<PreparedSourceSearchTextRequest, PreparedSourceResult>? Search { get; set; }
        public Task<PreparedSourceResult> ListAsync(PreparedSourceListRequest request, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(list(request)); }
        public Task<PreparedSourceResult> ReadTextRangeAsync(PreparedSourceReadTextRangeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PreparedSourceResult { RepositoryId = repository, Operation = PreparedSourceOperation.ReadTextRange, Outcome = PreparedSourceOutcome.Complete, Text = "source", ReadLocation = new(repository, request.RelativePath, 0, 6), ObservedFileByteLength = 6 });
        public Task<PreparedSourceResult> SearchTextAsync(PreparedSourceSearchTextRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult((Search ?? throw new InvalidOperationException("Unexpected search"))(request));
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    internal sealed class ControlledCodeGraph(Guid repository) : ICodeGraphWorkerToolSession
    {
        internal bool Disposed { get; private set; }
        internal bool Expired { get; set; }
        public Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken) => Task.FromResult(new CodeGraphToolReadiness(Expired ? CodeGraphToolReadinessStatus.Expired : CodeGraphToolReadinessStatus.Ready, repository, IssueCode: Expired ? CodeGraphToolIssueCode.SessionExpired : CodeGraphToolIssueCode.None));
        public Task<CodeGraphToolInvocationResult> ExploreAsync(string query, int maxFiles, CancellationToken cancellationToken) => Task.FromResult(new CodeGraphToolInvocationResult { RepositoryId = repository, Status = CodeGraphToolInvocationStatus.Complete });
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    internal sealed class ControlledJava(Guid repository) : IPreparedJavaNavigationContext
    {
        public Guid RepositoryId => repository;
        internal TaskCompletionSource? Started { get; set; }
        internal Task Release { get; set; } = Task.CompletedTask;
        internal bool Disposed { get; private set; }
        internal bool FailCleanup { get; set; }
        public Task<PreparedJavaNavigationResult> GetDefinitionAsync(PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default) => NavigateAsync(PreparedJavaNavigationOperation.Definition, cancellationToken);
        public Task<PreparedJavaNavigationResult> GetReferencesAsync(PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default) => NavigateAsync(PreparedJavaNavigationOperation.References, cancellationToken);
        private async Task<PreparedJavaNavigationResult> NavigateAsync(PreparedJavaNavigationOperation operation, CancellationToken token)
        {
            Started?.TrySetResult();
            await Release.WaitAsync(token);
            return new(repository, operation, PreparedJavaNavigationOutcome.Observed, [new(new(repository, "a.java"), 1, 1, 1, 2)], [], null);
        }
        public ValueTask DisposeAsync()
        {
            if (FailCleanup) return ValueTask.FromException(new InvalidOperationException("Controlled unconfirmed Java disposal"));
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
