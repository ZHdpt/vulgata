using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Source;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class OctoberScanPreparedSourceFrameTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acquire_UnicodeBomAndCrLfAcrossRanges_PreservesExactBytesAndLineCountAsync(bool bom)
    {
        var text = (bom ? "\uFEFF" : "") + "first\r\n中文😀\r\nlast";
        await using var harness = await FrameHarness.CreateAsync(text);
        var result = await harness.Runtime.AcquireFileFrameAsync("input.txt", new(PageByteCount: 7));
        Assert.Equal(OctoberSourceFrameStatus.Acquired, result.Status);
        var frame = Assert.IsType<OctoberPreparedSourceFrame>(result.Frame);
        Assert.Equal(text, frame.Text);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), frame.ByteLength);
        Assert.Equal(3, frame.LineCount);
        Assert.Equal(OctoberKnowledgePolicy.Hash(text), frame.Origin.SourceDigest);
        Assert.Equal(harness.Fixture.Approval.ApprovalIdentity, frame.Origin.PreparedVersion);
        Assert.Equal(harness.Fixture.Approval.RootBindingIdentity, frame.Origin.RootBindingIdentity);
        Assert.True(harness.Reads > 1);
        Assert.Equal(1, harness.Issues);
        Assert.All(harness.Contexts, context => Assert.True(context.Disposed));
        Assert.True(await frame.VerifyCurrentAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acquire_SameLengthRewriteBetweenRanges_NeverDisclosesMixedFrameAsync(bool restoreMetadata)
    {
        await using var harness = await FrameHarness.CreateAsync("AAAABBBBCCCCDDDD");
        var originalWrite = File.GetLastWriteTimeUtc(harness.Path);
        harness.AfterRead = async (number, _) =>
        {
            if (number != 1) return;
            await File.WriteAllTextAsync(harness.Path, "ZZZZYYYYXXXXXXXX", new UTF8Encoding(false));
            if (restoreMetadata) File.SetLastWriteTimeUtc(harness.Path, originalWrite);
        };
        if (restoreMetadata) harness.FixedObservedIdentity = "controlled-identical-file-metadata";
        var result = await harness.Runtime.AcquireFileFrameAsync("input.txt", new(PageByteCount: 4));
        Assert.Equal(OctoberSourceFrameStatus.SourceChanged, result.Status);
        Assert.Null(result.Frame);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("path")]
    [InlineData("offset")]
    [InlineData("length")]
    public async Task Acquire_ReturnedRangeBindingMismatch_RejectsWithoutFrameAsync(string fault)
    {
        await using var harness = await FrameHarness.CreateAsync("AAAABBBBCCCCDDDD");
        harness.RewriteResult = result => fault switch
        {
            "path" => result with { ReadLocation = result.ReadLocation! with { RelativePath = "other.txt" } },
            "offset" => result with { ReadLocation = result.ReadLocation! with { ByteStart = result.ReadLocation.ByteStart + 1 } },
            "length" => result with { ReadLocation = result.ReadLocation! with { ByteEndExclusive = result.ReadLocation.ByteEndExclusive + 1 } },
            _ => throw new InvalidOperationException(),
        };
        var result = await harness.Runtime.AcquireFileFrameAsync("input.txt", new(PageByteCount: 4));
        Assert.NotEqual(OctoberSourceFrameStatus.Acquired, result.Status);
        Assert.Null(result.Frame);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerifyCurrent_FrameWasAcquiredThenChangedOrRevoked_DeniesAsync(bool revoke)
    {
        await using var harness = await FrameHarness.CreateAsync("first revision");
        var result = await harness.Runtime.AcquireFileFrameAsync("input.txt");
        Assert.Equal(OctoberSourceFrameStatus.Acquired, result.Status);
        if (revoke) harness.Fixture.Authority.Revoke();
        else await File.WriteAllTextAsync(harness.Path, "other revision", new UTF8Encoding(false));
        Assert.False(await result.Frame!.VerifyCurrentAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Acquire_FileExceedsExplicitBound_ReturnsLimitedWithoutPartialFrameAsync()
    {
        await using var harness = await FrameHarness.CreateAsync("AAAABBBBCCCCDDDD");
        var result = await harness.Runtime.AcquireFileFrameAsync("input.txt", new(MaximumFileBytes: 8, PageByteCount: 4));
        Assert.Equal(OctoberSourceFrameStatus.Limited, result.Status);
        Assert.Null(result.Frame);
    }

    private sealed class FrameHarness : IAsyncDisposable
    {
        private readonly PreparedCodeGraphConnection _descriptor;
        private readonly PreparedSourceContextIssuer _issuer;
        private FrameHarness(PreparedSourceFixture fixture)
        {
            Fixture = fixture;
            fixture.Authority.Approve(fixture.Approval);
            _issuer = new(fixture.Authority);
            _descriptor = new(fixture.Approval.RepositoryId, System.IO.Path.Combine(fixture.SandboxPath, "never-executed.ps1"), "never-executed", [], fixture.PreparedRootPath, new Dictionary<string, string>(), static () => { });
            Runtime = new(fixture.Approval, TimeSpan.FromMinutes(5), new OctoberToolRuntimeDependencies(
                (_, _) => Task.FromResult(_descriptor),
                async (owner, _, token) =>
                {
                    Issues++;
                    var issued = await _issuer.IssueAsync(new(fixture.Approval, fixture.PreparedRootPath, PreparedSourceGrantLimits.Default), _descriptor, token);
                    if (issued.Context is null) return issued;
                    var context = new ObservedContext(this, issued.Context);
                    Contexts.Add(context);
                    return issued with { Context = context };
                },
                (context, path, token) => FixedObservedIdentity is { } identity ? Task.FromResult<string?>(identity) :
                    _issuer.ObserveFileIdentityAsync(((ObservedContext)context).Inner, path, token),
                (_, _) => throw new InvalidOperationException("Primary framing must not start Java"),
                (_, _) => throw new InvalidOperationException("Primary framing must not open a native session")));
        }
        internal PreparedSourceFixture Fixture { get; }
        internal OctoberToolRuntime Runtime { get; }
        internal string Path => System.IO.Path.Combine(Fixture.PreparedRootPath, "input.txt");
        internal int Issues { get; private set; }
        internal int Reads { get; private set; }
        internal string? FixedObservedIdentity { get; set; }
        internal Func<int, PreparedSourceResult, Task>? AfterRead { get; set; }
        internal Func<PreparedSourceResult, PreparedSourceResult>? RewriteResult { get; set; }
        internal List<ObservedContext> Contexts { get; } = [];
        internal static async Task<FrameHarness> CreateAsync(string text)
        {
            var fixture = await PreparedSourceFixture.CreateAsync();
            Directory.CreateDirectory(System.IO.Path.Combine(fixture.PreparedRootPath, ".codegraph"));
            var harness = new FrameHarness(fixture);
            await File.WriteAllTextAsync(harness.Path, text, new UTF8Encoding(false));
            return harness;
        }
        public async ValueTask DisposeAsync()
        {
            await Runtime.DisposeAsync();
            await _descriptor.DisposeAsync();
            await Fixture.DisposeAsync();
        }
        internal sealed class ObservedContext(FrameHarness owner, IPreparedSourceReadContext inner) : IPreparedSourceReadContext
        {
            internal IPreparedSourceReadContext Inner => inner;
            internal bool Disposed { get; private set; }
            public Guid RepositoryId => inner.RepositoryId;
            public PreparedSourceContextLifetime Lifetime => inner.Lifetime;
            public Task<PreparedSourceResult> ListAsync(PreparedSourceListRequest request, CancellationToken cancellationToken = default) => inner.ListAsync(request, cancellationToken);
            public Task<PreparedSourceResult> SearchTextAsync(PreparedSourceSearchTextRequest request, CancellationToken cancellationToken = default) => inner.SearchTextAsync(request, cancellationToken);
            public async Task<PreparedSourceResult> ReadTextRangeAsync(PreparedSourceReadTextRangeRequest request, CancellationToken cancellationToken = default)
            {
                var result = await inner.ReadTextRangeAsync(request, cancellationToken);
                owner.Reads++;
                if (owner.AfterRead is not null) await owner.AfterRead(owner.Reads, result);
                return owner.RewriteResult is not null && result.ReadLocation is not null ? owner.RewriteResult(result) : result;
            }
            public async ValueTask DisposeAsync() { await inner.DisposeAsync(); Disposed = true; }
        }
    }
}
