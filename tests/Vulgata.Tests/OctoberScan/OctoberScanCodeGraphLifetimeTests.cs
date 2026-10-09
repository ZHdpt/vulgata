using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Vulgata.Tests.PreparedSource;
using Vulgata.Infrastructure.Source;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Controlled descriptor lifecycle tests. Creating/disposal without discovery never starts a native process.</summary>
public sealed class OctoberScanCodeGraphLifetimeTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenSession_DisposedCurrentSession_AllowsReplacementOnSameDescriptorAsync()
    {
        var releases = 0;
        await using var descriptor = CreateDescriptor(() => releases++);
        var first = descriptor.OpenSession();
        await first.DisposeAsync();
        var second = descriptor.OpenSession();
        Assert.NotSame(first, second);
        Assert.Equal(0, releases);
        await second.DisposeAsync();
        await descriptor.DisposeAsync();
        Assert.Equal(1, releases);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenSession_CurrentSessionStillOwned_RejectsConcurrentReplacementAsync()
    {
        await using var descriptor = CreateDescriptor(static () => { });
        await using var first = descriptor.OpenSession();
        Assert.Throws<InvalidOperationException>(() => descriptor.OpenSession());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenSession_TwentyReplacements_RejectsFurtherRenewalAsync()
    {
        await using var descriptor = CreateDescriptor(static () => { });
        // Initial session plus twenty replacements; no discovery or native transport.
        for (var index = 0; index <= 20; index++)
        {
            var session = descriptor.OpenSession();
            await session.DisposeAsync();
        }
        Assert.Throws<InvalidOperationException>(() => descriptor.OpenSession());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenSession_PendingCleanup_RejectsReplacementUntilSettlementAsync()
    {
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new ControlledSession { Cleanup = () => cleanup.Task };
        await using var descriptor = new PreparedCodeGraphConnection(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "never-executed.ps1"), "never-executed", [], Path.GetTempPath(), new Dictionary<string, string>(), static () => { }, _ => inner);
        var first = descriptor.OpenSession();
        var disposing = first.DisposeAsync().AsTask();
        Assert.False(disposing.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => descriptor.OpenSession());
        cleanup.SetResult();
        await disposing;
        await using var replacement = descriptor.OpenSession();
        Assert.NotSame(first, replacement);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_FailedCleanup_RemainsFailedAndNeverReleasesOwnerAsync()
    {
        var releases = 0;
        var inner = new ControlledSession { Cleanup = () => Task.FromException(new InvalidOperationException("Controlled unconfirmed cleanup")) };
        var descriptor = new PreparedCodeGraphConnection(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "never-executed.ps1"), "never-executed", [], Path.GetTempPath(), new Dictionary<string, string>(), () => releases++, _ => inner);
        descriptor.OpenSession();
        await Assert.ThrowsAsync<InvalidOperationException>(() => descriptor.DisposeAsync().AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => descriptor.DisposeAsync().AsTask());
        Assert.Equal(0, releases);
        Assert.Throws<ObjectDisposedException>(() => descriptor.OpenSession());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_ContainedVerifierMatches_UsesExactDescriptorAndFinalReadAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        await using var client = harness.CreateClient();
        Assert.True((await client.DiscoverAsync(CancellationToken.None)).IsReady);
        var result = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Complete, result.Status);
        Assert.Equal("notes.oddlangx", Assert.Single(result.Excerpts).Location.RelativePath);
        Assert.Equal(harness.Descriptor.RepositoryId, result.RepositoryId);
        Assert.True(harness.Source.Reads >= 2, "Contained evidence and a final live read must precede disclosure.");
        Assert.Equal(harness.Descriptor.WorkingDirectory, harness.Transport.LastArguments!["projectPath"]);
        Assert.Equal(1, harness.Transport.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExploreAsync_WrongRepositoryOrAuthorityDenied_ReturnsNoExcerptAsync(bool wrongRepository)
    {
        await using var harness = await NativeHarness.CreateAsync();
        if (wrongRepository)
            harness.Source.RepositoryId = Guid.NewGuid();
        else
            harness.IssueStatus = PreparedSourceIssueStatus.Denied;
        await using var client = harness.CreateClient();
        await client.DiscoverAsync(CancellationToken.None);
        var result = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Denied, result.Status);
        Assert.Empty(result.Excerpts);
        Assert.Empty(result.Locations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_RevokedAfterVerification_DiscardsEarlierMatchingExcerptAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        harness.Source.DenyAfterFirstRead = true;
        await using var client = harness.CreateClient();
        await client.DiscoverAsync(CancellationToken.None);
        var result = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Denied, result.Status);
        Assert.Empty(result.Excerpts);
        Assert.Empty(result.Locations);
        Assert.True(harness.Source.Reads >= 2);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_PhysicalFileMatchesButContainedContentDoesNot_NeverFallsBackToHostReadAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        harness.Source.Content = "different current contained bytes";
        await using var client = harness.CreateClient();
        await client.DiscoverAsync(CancellationToken.None);
        var result = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Limited, result.Status);
        Assert.Equal(CodeGraphToolIssueCode.IncompleteIndex, result.IssueCode);
        Assert.Empty(result.Excerpts);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_NativeOutputOverflow_DoesNotReadOrDiscloseSourceAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        harness.Transport.Text = new string('x', 256 * 1024 + 1);
        await using var client = harness.CreateClient();
        await client.DiscoverAsync(CancellationToken.None);
        var result = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Limited, result.Status);
        Assert.Equal(CodeGraphToolIssueCode.ResultLimit, result.IssueCode);
        Assert.Empty(result.Excerpts);
        Assert.Equal(0, harness.Source.Reads);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_EscapeInNativeResult_DeniesWithoutContainedReadAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        harness.Transport.Text = NativeHarness.Output("../outside/sentinel.txt");
        await using var client = harness.CreateClient();
        await client.DiscoverAsync(CancellationToken.None);
        var result = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Denied, result.Status);
        Assert.Empty(result.Excerpts);
        Assert.Equal(0, harness.Source.Reads);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_TenthCallThenRenewalAttempt_StopsAtTenNativeInvocationsAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        await using var client = harness.CreateClient();
        Assert.True((await client.DiscoverAsync(CancellationToken.None)).IsReady);
        for (var index = 0; index < 10; index++)
            Assert.Equal(CodeGraphToolInvocationStatus.Complete, (await client.ExploreAsync("source", 3, CancellationToken.None)).Status);
        var exhausted = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Limited, exhausted.Status);
        Assert.Equal(CodeGraphToolIssueCode.ResultLimit, exhausted.IssueCode);
        Assert.Equal(10, harness.Transport.Calls);
        Assert.Empty(exhausted.Excerpts);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_TwoMinuteSessionExpiresDespiteWallClockRollback_StopsBeforeNativeInvocationAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        await using var client = harness.CreateClient();
        Assert.True((await client.DiscoverAsync(CancellationToken.None)).IsReady);
        harness.Clock.Advance(TimeSpan.FromMinutes(2));
        harness.Clock.RollBackWallClock(TimeSpan.FromDays(1));
        var result = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Expired, result.Status);
        Assert.Empty(result.Excerpts);
        Assert.Equal(0, harness.Transport.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DiscoverAsync_UnreviewedNativeSchema_RejectsWithoutSourceFallbackAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        harness.Transport.ToolName = "different-tool";
        await using var client = harness.CreateClient();
        var readiness = await client.DiscoverAsync(CancellationToken.None);
        Assert.Equal(CodeGraphToolReadinessStatus.DiscoveryFailed, readiness.Status);
        Assert.Equal(CodeGraphToolIssueCode.ToolMissing, readiness.IssueCode);
        Assert.Equal(0, harness.Transport.Calls);
        Assert.Equal(0, harness.Source.Reads);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_ContainedVerificationWaits_UsesSameBoundedOperationCancellationAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        harness.Source.WaitUntilCancelled = true;
        await using var client = harness.CreateClient(TimeSpan.FromMilliseconds(100));
        Assert.True((await client.DiscoverAsync(CancellationToken.None)).IsReady);
        using var caller = new CancellationTokenSource();
        var pending = client.ExploreAsync("source", 3, caller.Token);
        try
        {
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(CodeGraphToolInvocationStatus.TimedOut, result.Status);
            Assert.Equal(CodeGraphToolIssueCode.DeadlineExceeded, result.IssueCode);
            Assert.Empty(result.Excerpts);
            Assert.False(caller.IsCancellationRequested);
        }
        finally
        {
            caller.Cancel();
            await pending;
        }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExploreAsync_RetainedSessionAuthorityRevoked_RejectsBeforeNativeCallAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        await using var client = harness.CreateClient();
        Assert.True((await client.DiscoverAsync(CancellationToken.None)).IsReady);
        harness.IssueStatus = PreparedSourceIssueStatus.Denied;
        var result = await client.ExploreAsync("source", 3, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Denied, result.Status);
        Assert.Empty(result.Excerpts);
        Assert.Equal(0, harness.Transport.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DiscoverAsync_AuthorityAlreadyDenied_RejectsBeforeNativeConnectAsync()
    {
        await using var harness = await NativeHarness.CreateAsync();
        harness.IssueStatus = PreparedSourceIssueStatus.Denied;
        await using var client = harness.CreateClient();
        var readiness = await client.DiscoverAsync(CancellationToken.None);
        Assert.Equal(CodeGraphToolReadinessStatus.AuthorityDenied, readiness.Status);
        Assert.Equal(0, harness.Connects);
        Assert.Equal(0, harness.Transport.Calls);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualContainedAdapterCompleteLfExcerpt_IsAdmittedForLfAndCrLfSourceAsync(bool crlf)
    {
        await using var native = await NativeHarness.CreateAsync();
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        native.Source.Content = crlf ? "café\r\nsource fixture\r\n" : "café\nsource fixture\n";
        await using var client = native.CreateClient();
        Assert.True((await client.DiscoverAsync(CancellationToken.None)).IsReady);
        var actual = await client.ExploreAsync("source", 4, CancellationToken.None);
        Assert.Equal(CodeGraphToolInvocationStatus.Complete, actual.Status);
        Assert.Equal(native.Descriptor.RepositoryId, actual.RepositoryId);
        var excerpt = Assert.Single(actual.Excerpts);
        Assert.Equal("café\nsource fixture", excerpt.Text);
        Assert.Null(actual.Text);
        Assert.Equal(!crlf, native.Source.Content.Contains(excerpt.Text, StringComparison.Ordinal));
        Assert.True(native.Source.Content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Contains(excerpt.Text, StringComparison.Ordinal));
        Assert.Null(excerpt.LineStart);
        Assert.Null(excerpt.LineEnd);
        Assert.Equal(excerpt.Location, Assert.Single(actual.Locations));
        Assert.Equal(1, native.Transport.Calls);
        Assert.True(native.Source.Reads >= 2);

        // Actual contained parser with inert transport; controlled frame/authority only.
        // The request is rebound solely for ledger admission, never accepted or published.
        var origin = host.Source.Origin with
        {
            RepositoryId = native.Descriptor.RepositoryId, RelativePath = excerpt.Location.RelativePath,
            SourceDigest = Vulgata.Core.DomainServices.OctoberKnowledge.OctoberKnowledgePolicy.Hash(native.Source.Content)
        };
        var frame = new ParserLedgerFrame(origin, native.Source.Content);
        var source = new Vulgata.Core.DomainServices.OctoberKnowledge.Contributor(
            Vulgata.Core.DomainServices.OctoberKnowledge.ContributorKind.Source, origin.RepositoryId,
            null, origin.RootBindingIdentity, null, origin.SourceDigest);
        var request = host.Request with { Source = frame, Contributors = [source], Advisory = [] };
        int unexpectedAcquisitions = 0;
        var ledger = new Vulgata.OctoberGenerator.OctoberWorkerInputLedger(request,
            (_, _) => { unexpectedAcquisitions++; return Task.FromResult<Vulgata.Agents.WorkerExecution.IOctoberVerifiedFileFrame?>(null); },
            (_, _) => Task.FromResult(Vulgata.Core.DomainServices.OctoberKnowledge.DisclosureStatus.Allowed));
        var admission = await ledger.AdmitToolResultAsync(
            Vulgata.Core.DomainServices.OctoberKnowledge.SourceMetadataOperation.CodeGraphExplore,
            ".", "{\"query\":\"source\",\"maxFiles\":4}", actual);
        Assert.Equal(Vulgata.Core.DomainServices.OctoberKnowledge.DisclosureStatus.Allowed, admission.Status);
        Assert.Equal(0, unexpectedAcquisitions);
        Assert.Equal(origin.SourceDigest, Vulgata.Core.DomainServices.OctoberKnowledge.OctoberKnowledgePolicy.Hash(frame.Text));
        Assert.Equal(native.Source.Content, frame.Text);
        Assert.Equal(excerpt, Assert.Single(Assert.IsType<CodeGraphToolInvocationResult>(admission.Result).Excerpts));
        var snapshot = Assert.IsType<Vulgata.OctoberGenerator.OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Equal(origin, Assert.Single(snapshot.VerifiedSources).Origin);
        Assert.Contains(source, snapshot.Contributors);
        Assert.Single(snapshot.Contributors.Where(item => item.Kind == Vulgata.Core.DomainServices.OctoberKnowledge.ContributorKind.SourceMetadata));
        Assert.True(await ledger.VerifySnapshotAsync(snapshot));
    }

    private sealed class ParserLedgerFrame(
        Vulgata.Core.DomainServices.OctoberKnowledge.PreparedOrigin origin, string text)
        : Vulgata.Agents.WorkerExecution.IOctoberVerifiedFileFrame
    {
        public Vulgata.Core.DomainServices.OctoberKnowledge.PreparedOrigin Origin => origin;
        public string Text => text;
        public long ByteLength => Encoding.UTF8.GetByteCount(Text);
        public int LineCount => 3;
        public string FileIdentity => "controlled-parser-ledger-frame";
        public Task<bool> VerifyCurrentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }
    private sealed class NativeHarness : IAsyncDisposable
    {
        private readonly PreparedSourceFixture _fixture;
        private NativeHarness(PreparedSourceFixture fixture, PreparedCodeGraphConnection descriptor)
        {
            _fixture = fixture;
            Descriptor = descriptor;
            Source = new ControlledReadContext { RepositoryId = descriptor.RepositoryId };
        }
        internal PreparedCodeGraphConnection Descriptor { get; }
        internal ControlledReadContext Source { get; }
        internal ControlledTransport Transport { get; } = new();
        internal ControlledClock Clock { get; } = new();
        internal PreparedSourceIssueStatus IssueStatus { get; set; } = PreparedSourceIssueStatus.Issued;
        internal int Connects { get; private set; }
        internal static string Output(string path = "notes.oddlangx") => "**Source Code**\n**`" + path + "`**\n```text\ncafé\nsource fixture\n```";
        internal static async Task<NativeHarness> CreateAsync()
        {
            var fixture = await PreparedSourceFixture.CreateAsync();
            Directory.CreateDirectory(Path.Combine(fixture.PreparedRootPath, ".codegraph"));
            var command = Path.Combine(fixture.SandboxPath, "controlled-never-executed.ps1");
            await File.WriteAllTextAsync(command, "# Controlled transport injected; never executed");
            var descriptor = new PreparedCodeGraphConnection(fixture.Approval.RepositoryId, command, "descriptor-command", ["descriptor-argument"], fixture.PreparedRootPath, new Dictionary<string, string>(), static () => { });
            return new(fixture, descriptor);
        }
        internal NativeCodeGraphMcpClient CreateClient(TimeSpan? operationTimeout = null) => new(Descriptor, Clock,
            _ => Task.FromResult(new PreparedSourceIssueResult(IssueStatus, IssueStatus == PreparedSourceIssueStatus.Issued ? Source : null)),
            _ => { Connects++; return Task.FromResult<ICodeGraphMcpTransport>(Transport); }, operationTimeout);
        public async ValueTask DisposeAsync()
        {
            await Descriptor.DisposeAsync();
            await _fixture.DisposeAsync();
        }
    }

    private sealed class ControlledClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        private long _elapsedTicks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _elapsedTicks;
        internal void Advance(TimeSpan amount) { _now += amount; _elapsedTicks += amount.Ticks; }
        internal void RollBackWallClock(TimeSpan amount) => _now -= amount;
    }

    private sealed class ControlledTransport : ICodeGraphMcpTransport
    {
        internal string Text { get; set; } = NativeHarness.Output();
        internal string ToolName { get; set; } = "codegraph_explore";
        internal int Calls { get; private set; }
        internal IReadOnlyDictionary<string, object?>? LastArguments { get; private set; }
        public Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Tool>>([new Tool
            {
                Name = ToolName,
                InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { query = new { type = "string" }, maxFiles = new { type = "number" }, projectPath = new { type = "string" } }, required = new[] { "query" } }),
            }]);
        }
        public Task<CallToolResult> CallToolAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastArguments = arguments;
            return Task.FromResult(new CallToolResult { Content = [new TextContentBlock { Text = Text }] });
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ControlledReadContext : IPreparedSourceReadContext
    {
        public Guid RepositoryId { get; set; }
        public PreparedSourceContextLifetime Lifetime => PreparedSourceContextLifetime.Open;
        internal int Reads { get; private set; }
        internal string Content { get; set; } = "café\nsource fixture\n";
        internal bool DenyAfterFirstRead { get; set; }
        internal bool WaitUntilCancelled { get; set; }
        private static async Task<PreparedSourceResult> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Controlled read should only stop by cancellation");
        }
        public Task<PreparedSourceResult> ListAsync(PreparedSourceListRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Verifier must use contained ranges");
        public Task<PreparedSourceResult> SearchTextAsync(PreparedSourceSearchTextRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Verifier must use contained ranges");
        public Task<PreparedSourceResult> ReadTextRangeAsync(PreparedSourceReadTextRangeRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            if (WaitUntilCancelled) return WaitForCancellationAsync(cancellationToken);
            if (DenyAfterFirstRead && Reads > 1)
                return Task.FromResult(new PreparedSourceResult { RepositoryId = RepositoryId, Operation = PreparedSourceOperation.ReadTextRange, Outcome = PreparedSourceOutcome.Denied });
            Assert.Equal("notes.oddlangx", request.RelativePath);
            var bytes = Encoding.UTF8.GetBytes(Content);
            var start = checked((int)request.ByteOffset);
            var count = Math.Min(request.ByteCount, bytes.Length - start);
            return Task.FromResult(new PreparedSourceResult
            {
                RepositoryId = RepositoryId, Operation = PreparedSourceOperation.ReadTextRange,
                Outcome = PreparedSourceOutcome.Complete, Text = Encoding.UTF8.GetString(bytes.AsSpan(start, count)),
                ObservedFileByteLength = bytes.Length, ReadLocation = new(RepositoryId, request.RelativePath, start, start + count),
            });
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrepareAsync_RootOrStateReplacementDuringInit_IsPreventedOrRejectedWithoutAdoptionAsync(bool replaceRoot)
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var command = Path.Combine(fixture.SandboxPath, "never-executed.ps1");
        await File.WriteAllTextAsync(command, "# controlled init runner only");
        var root = fixture.PreparedRootPath;
        var movePreventedByHeldHandle = false;
        var runner = new ControlledInitRunner(request =>
        {
            if (replaceRoot)
            {
                try { Directory.Move(root, Path.Combine(fixture.SandboxPath, "original-root")); }
                catch (IOException) { movePreventedByHeldHandle = true; throw; }
                Directory.CreateDirectory(root);
            }
            else
                Directory.Move(Path.Combine(root, ".codegraph"), Path.Combine(root, "original-state"));
            Directory.CreateDirectory(Path.Combine(root, ".codegraph"));
            File.WriteAllText(Path.Combine(root, ".codegraph", "codegraph.db"), "replacement must not be adopted");
        });
        var factory = new PreparedCodeGraphConnectionFactory(command, runner, static (_, _) => Task.CompletedTask);
        var rejection = await Record.ExceptionAsync(async () =>
        {
            await using var unexpected = await factory.PrepareAsync(fixture.Approval.RepositoryId, root, CancellationToken.None);
        });
        Assert.Equal(1, runner.Calls);
        if (movePreventedByHeldHandle)
        {
            Assert.True(replaceRoot);
            Assert.IsType<IOException>(rejection);
            Assert.Equal("café\nsource fixture\n", await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
            Assert.False(Directory.Exists(Path.Combine(fixture.SandboxPath, "original-root")));
        }
        else
        {
            Assert.IsType<InvalidOperationException>(rejection);
            Assert.Equal("replacement must not be adopted", await File.ReadAllTextAsync(Path.Combine(root, ".codegraph", "codegraph.db")));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_RemainingRunShorterThanInitCap_BoundsProcessAndCancellationAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var command = Path.Combine(fixture.SandboxPath, "never-executed.ps1");
        await File.WriteAllTextAsync(command, "# controlled runner only");
        var remaining = TimeSpan.FromSeconds(10);
        var runner = new ControlledInitRunner(request =>
        {
            Assert.True(request.Timeout > TimeSpan.Zero && request.Timeout <= remaining);
            File.WriteAllText(Path.Combine(request.WorkingDirectory, ".codegraph", "codegraph.db"), "controlled index");
        });
        var factory = new PreparedCodeGraphConnectionFactory(command, runner, static (_, _) => Task.CompletedTask);
        await using var descriptor = await factory.PrepareAsync(fixture.Approval.RepositoryId, fixture.PreparedRootPath, remaining, CancellationToken.None);
        Assert.Equal(1, runner.Calls);
        Assert.True(runner.LastToken.CanBeCanceled, "Init needs a linked finite deadline even without caller cancellation.");
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_PreflightReplacesRoot_RejectsBeforeInitProcessAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var command = Path.Combine(fixture.SandboxPath, "never-executed.ps1");
        await File.WriteAllTextAsync(command, "# controlled runner only");
        var runner = new ControlledInitRunner(request => File.WriteAllText(Path.Combine(request.WorkingDirectory, ".codegraph", "codegraph.db"), "must not run"));
        var factory = new PreparedCodeGraphConnectionFactory(command, runner, (_, _) =>
        {
            Directory.Move(fixture.PreparedRootPath, Path.Combine(fixture.SandboxPath, "original-preflight-root"));
            Directory.CreateDirectory(fixture.PreparedRootPath);
            File.WriteAllText(Path.Combine(fixture.PreparedRootPath, "replacement.txt"), "preserve source");
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var unexpected = await factory.PrepareAsync(fixture.Approval.RepositoryId, fixture.PreparedRootPath, CancellationToken.None);
        });
        Assert.Equal(0, runner.Calls);
        Assert.Equal("preserve source", await File.ReadAllTextAsync(Path.Combine(fixture.PreparedRootPath, "replacement.txt")));
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_ForeignStateAppearsAfterAbsenceCheck_DoesNotAdoptOrInitializeItAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var command = Path.Combine(fixture.SandboxPath, "never-executed.ps1");
        await File.WriteAllTextAsync(command, "# controlled runner only");
        var state = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        var marker = Path.Combine(state, "foreign-owner.txt");
        var runner = new ControlledInitRunner(request => File.WriteAllText(Path.Combine(request.WorkingDirectory, ".codegraph", "codegraph.db"), "must never initialize foreign state"));
        var factory = new PreparedCodeGraphConnectionFactory(command, runner, static (_, _) => Task.CompletedTask,
            beforeStateCreate: root =>
            {
                Assert.Equal(Path.GetFullPath(fixture.PreparedRootPath), root);
                Directory.CreateDirectory(state);
                File.WriteAllText(marker, "foreign state must be preserved");
            });
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var unexpected = await factory.PrepareAsync(fixture.Approval.RepositoryId, fixture.PreparedRootPath, CancellationToken.None);
        });
        Assert.Equal(0, runner.Calls);
        Assert.Equal("foreign state must be preserved", await File.ReadAllTextAsync(marker));
        Assert.False(File.Exists(Path.Combine(state, "codegraph.db")));
    }
    private sealed class ControlledInitRunner(Action<CodeGraphProcessRequest> duringInit) : ICodeGraphProcessRunner
    {
        internal int Calls { get; private set; }
        internal CancellationToken LastToken { get; private set; }
        public Task<CodeGraphProcessResult> RunAsync(CodeGraphProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastToken = cancellationToken;
            duringInit(request);
            return Task.FromResult(new CodeGraphProcessResult(0, [], [], TimedOut: false));
        }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenSession_RunDeadlineExpiresDespiteWallClockRollback_DeniesRenewalAndSourceAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        Directory.CreateDirectory(Path.Combine(fixture.PreparedRootPath, ".codegraph"));
        var clock = new ControlledClock();
        await using var owner = new PreparedCodeGraphConnection(fixture.Approval.RepositoryId, Path.Combine(fixture.SandboxPath, "never-executed.ps1"), "never-executed", [], fixture.PreparedRootPath, new Dictionary<string, string>(), static () => { }, _ => new ControlledSession(), remainingLifetime: TimeSpan.FromSeconds(10), timeProvider: clock);
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        var request = new PreparedSourceGrantRequest(fixture.Approval, fixture.PreparedRootPath, PreparedSourceGrantLimits.Default);
        var issued = await issuer.IssueAsync(request, owner);
        Assert.Equal(PreparedSourceIssueStatus.Issued, issued.Status);
        await using var context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(issued.Context);
        var first = owner.OpenSession();
        await first.DisposeAsync();
        clock.Advance(TimeSpan.FromSeconds(10));
        clock.RollBackWallClock(TimeSpan.FromDays(1));
        Assert.Throws<InvalidOperationException>(() => owner.OpenSession());
        var stale = await issuer.IssueAsync(request, owner);
        Assert.NotEqual(PreparedSourceIssueStatus.Issued, stale.Status);
        Assert.Null(stale.Context);
        var read = await context.ReadTextRangeAsync(new("notes.oddlangx", 0, 64));
        Assert.Null(read.Text);
        Assert.NotEqual(PreparedSourceOutcome.Complete, read.Outcome);
    }
    private sealed class ControlledSession : ICodeGraphWorkerToolSession
    {
        internal Func<Task> Cleanup { get; init; } = () => Task.CompletedTask;
        public Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Not used by lifecycle probe");
        public Task<CodeGraphToolInvocationResult> ExploreAsync(string query, int maxFiles, CancellationToken cancellationToken) => throw new InvalidOperationException("Not used by lifecycle probe");
        public ValueTask DisposeAsync() => new(Cleanup());
    }
    private static PreparedCodeGraphConnection CreateDescriptor(Action release) => new(
        Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "never-executed-codegraph.ps1"),
        "descriptor-selected-command", ["descriptor-selected-argument"], Path.GetTempPath(),
        new Dictionary<string, string> { ["CODEGRAPH_NO_DOWNLOAD"] = "1" }, release);
}
