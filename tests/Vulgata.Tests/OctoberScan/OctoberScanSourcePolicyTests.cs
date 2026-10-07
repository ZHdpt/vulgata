using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class OctoberScanSourcePolicyTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SourceOperations_UnownedSameNameDirectory_RemainsVisibleAndReadableAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        Directory.CreateDirectory(Path.Combine(fixture.PreparedRootPath, ".codegraph"));
        await File.WriteAllTextAsync(Path.Combine(fixture.PreparedRootPath, ".codegraph", "source.txt"), "ordinary source marker");
        await using var context = await IssueAsync(fixture);
        var listed = await context.ListAsync(new("", 100));
        Assert.Contains(listed.Entries, entry => entry.Location.RelativePath == ".codegraph");
        var read = await context.ReadTextRangeAsync(new(".codegraph/source.txt", 0, 64));
        Assert.Equal(PreparedSourceOutcome.Complete, read.Outcome);
        Assert.Equal("ordinary source marker", read.Text);
        var searched = await context.SearchTextAsync(new(".codegraph", "ordinary source", 10));
        Assert.Single(searched.Matches);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_CursorTransferredOrReplayed_IsDeniedAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        await using var first = await IssueAsync(fixture);
        await using var other = await IssueAsync(fixture);
        var page = await first.ListAsync(new("", 1));
        Assert.NotNull(page.Continuation);
        var foreign = await other.ListAsync(new("", 1, page.Continuation));
        Assert.Equal(PreparedSourceOutcome.Denied, foreign.Outcome);
        Assert.Empty(foreign.Entries);
        var next = await first.ListAsync(new("", 1, page.Continuation));
        Assert.Single(next.Entries);
        Assert.NotEqual(page.Entries[0].Location.RelativePath, next.Entries[0].Location.RelativePath);
        var replay = await first.ListAsync(new("", 1, page.Continuation));
        Assert.Equal(PreparedSourceOutcome.Denied, replay.Outcome);
        Assert.Empty(replay.Entries);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SourceOperations_TraversalEscapeOrLiveRevocation_ReturnsNoPayloadAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        await using var context = await IssueAsync(fixture);
        var escape = await context.ReadTextRangeAsync(new("../outside/sentinel.txt", 0, 64));
        Assert.Equal(PreparedSourceOutcome.Denied, escape.Outcome);
        Assert.Null(escape.Text);
        fixture.Authority.Revoke();
        var listed = await context.ListAsync(new("", 100));
        var searched = await context.SearchTextAsync(new("", "source", 10));
        var read = await context.ReadTextRangeAsync(new("notes.oddlangx", 0, 64));
        Assert.Equal(PreparedSourceOutcome.Denied, listed.Outcome);
        Assert.Equal(PreparedSourceOutcome.Denied, searched.Outcome);
        Assert.Equal(PreparedSourceOutcome.Denied, read.Outcome);
        Assert.Empty(listed.Entries);
        Assert.Empty(searched.Matches);
        Assert.Null(read.Text);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SourceOperations_PositiveOwnedIdentity_ExcludesStateBeforeBudgetsAndDeniesDirectReadAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var root = Path.Combine(fixture.SandboxPath, "owned-root");
        Directory.CreateDirectory(Path.Combine(root, ".codegraph"));
        await File.WriteAllTextAsync(Path.Combine(root, "real.txt"), "source marker");
        await File.WriteAllTextAsync(Path.Combine(root, ".codegraph", "codegraph.db"), new string('x', 4096) + "source marker");
        await using var owner = CreateOwner(fixture, root);
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        var limits = PreparedSourceGrantLimits.Default with { MaximumMetadataEntries = 1, MaximumExaminedBytes = 64 };
        var issued = await issuer.IssueAsync(new(fixture.Approval, root, limits), owner);
        Assert.Equal(PreparedSourceIssueStatus.Issued, issued.Status);
        await using var context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(issued.Context);
        var listing = await context.ListAsync(new("", 100));
        Assert.Equal(PreparedSourceOutcome.Complete, listing.Outcome);
        Assert.Equal("real.txt", Assert.Single(listing.Entries).Location.RelativePath);
        var reading = await context.ReadTextRangeAsync(new(".codegraph/codegraph.db", 0, 32));
        Assert.Equal(PreparedSourceOutcome.Denied, reading.Outcome);
        Assert.Null(reading.Text);
        // Separate normal grant, so listing's metadata accounting cannot mask search exclusion.
        var searchIssue = await issuer.IssueAsync(new(fixture.Approval, root, limits), owner);
        await using var searchContext = Assert.IsAssignableFrom<IPreparedSourceReadContext>(searchIssue.Context);
        var search = await searchContext.SearchTextAsync(new("", "source marker", 10));
        Assert.Equal(PreparedSourceOutcome.Complete, search.Outcome);
        Assert.Equal("real.txt", Assert.Single(search.Matches).Location.RelativePath);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssueAsync_OwnerWrongRootOrRepository_DeniesCapabilityAsync(bool wrongRepository)
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        Directory.CreateDirectory(Path.Combine(fixture.PreparedRootPath, ".codegraph"));
        await using var owner = CreateOwner(fixture, fixture.PreparedRootPath, wrongRepository ? Guid.NewGuid() : null);
        fixture.Authority.Approve(fixture.Approval);
        var result = await new PreparedSourceContextIssuer(fixture.Authority).IssueAsync(
            new(fixture.Approval, wrongRepository ? fixture.PreparedRootPath : fixture.SiblingPath, PreparedSourceGrantLimits.Default), owner);
        Assert.NotEqual(PreparedSourceIssueStatus.Issued, result.Status);
        Assert.Null(result.Context);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceOperations_OwnerDisposedOrStateReplaced_ClosesDisclosureAsync(bool replaced)
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var state = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(state);
        await using var owner = CreateOwner(fixture, fixture.PreparedRootPath);
        fixture.Authority.Approve(fixture.Approval);
        var issue = await new PreparedSourceContextIssuer(fixture.Authority).IssueAsync(new(fixture.Approval, fixture.PreparedRootPath, PreparedSourceGrantLimits.Default), owner);
        await using var context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(issue.Context);
        if (replaced)
        {
            Directory.Move(state, Path.Combine(fixture.PreparedRootPath, "old-state"));
            Directory.CreateDirectory(state);
            await File.WriteAllTextAsync(Path.Combine(state, "replacement.txt"), "not the owned object");
        }
        else
            await owner.DisposeAsync();
        var listing = await context.ListAsync(new("", 100));
        var read = await context.ReadTextRangeAsync(new("notes.oddlangx", 0, 64));
        Assert.DoesNotContain(listing.Outcome, new[] { PreparedSourceOutcome.Complete, PreparedSourceOutcome.CompleteEmpty, PreparedSourceOutcome.Limited });
        Assert.Empty(listing.Entries);
        Assert.Null(read.Text);
        Assert.NotEqual(PreparedSourceOutcome.Complete, read.Outcome);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SourceOperations_OwnedStateReplacedByOutsideLink_ClosesWithoutFollowingLinkAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var state = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(state);
        await using var owner = CreateOwner(fixture, fixture.PreparedRootPath);
        fixture.Authority.Approve(fixture.Approval);
        var issue = await new PreparedSourceContextIssuer(fixture.Authority).IssueAsync(new(fixture.Approval, fixture.PreparedRootPath, PreparedSourceGrantLimits.Default), owner);
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        await using var context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(issue.Context);
        Directory.Move(state, Path.Combine(fixture.PreparedRootPath, "original-state"));
        Directory.CreateSymbolicLink(state, fixture.SiblingPath);
        var result = await context.ReadTextRangeAsync(new(".codegraph/sentinel.txt", 0, 64));
        Assert.Null(result.Text);
        Assert.NotEqual(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal("outside the approved prepared root", await File.ReadAllTextAsync(fixture.SentinelPath));
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadTextRangeAsync_SameLengthRewriteOrReplacementBetweenRanges_DeniesMixedRevisionAsync(bool replaceFile)
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var path = Path.Combine(fixture.PreparedRootPath, "changing.txt");
        await File.WriteAllTextAsync(path, "AAAA BBBB");
        await using var context = await IssueAsync(fixture);
        var first = await context.ReadTextRangeAsync(new("changing.txt", 0, 4));
        Assert.Equal("AAAA", first.Text);
        if (replaceFile)
            File.Move(path, Path.Combine(fixture.PreparedRootPath, "original.txt"));
        await File.WriteAllTextAsync(path, "CCCC DDDD");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(1));
        var second = await context.ReadTextRangeAsync(new("changing.txt", 5, 4));
        Assert.Equal(PreparedSourceOutcome.ChangedInput, second.Outcome);
        Assert.Null(second.Text);
        Assert.Null(second.ReadLocation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SourceOperations_SessionCleanupUnconfirmed_ClosesExistingAndNewGrantsAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        Directory.CreateDirectory(Path.Combine(fixture.PreparedRootPath, ".codegraph"));
        var owner = new PreparedCodeGraphConnection(fixture.Approval.RepositoryId, Path.Combine(fixture.SandboxPath, "never-executed.ps1"), "never-executed", [], fixture.PreparedRootPath, new Dictionary<string, string>(), static () => { }, _ => new UnsettledSession());
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        var request = new PreparedSourceGrantRequest(fixture.Approval, fixture.PreparedRootPath, PreparedSourceGrantLimits.Default);
        var issued = await issuer.IssueAsync(request, owner);
        Assert.Equal(PreparedSourceIssueStatus.Issued, issued.Status);
        await using var context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(issued.Context);
        var session = owner.OpenSession();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DisposeAsync().AsTask());
        var secondGrant = await issuer.IssueAsync(request, owner);
        Assert.NotEqual(PreparedSourceIssueStatus.Issued, secondGrant.Status);
        Assert.Null(secondGrant.Context);
        var existing = await context.ReadTextRangeAsync(new("notes.oddlangx", 0, 64));
        Assert.Null(existing.Text);
        Assert.NotEqual(PreparedSourceOutcome.Complete, existing.Outcome);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.DisposeAsync().AsTask());
    }

    private sealed class UnsettledSession : ICodeGraphWorkerToolSession
    {
        public Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Not called");
        public Task<CodeGraphToolInvocationResult> ExploreAsync(string query, int maxFiles, CancellationToken cancellationToken) => throw new InvalidOperationException("Not called");
        public ValueTask DisposeAsync() => ValueTask.FromException(new InvalidOperationException("Controlled unconfirmed cleanup"));
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenRelativeFile_ExcludedDirectoryObjectUnderOrdinarySpelling_DeniesIdentityRatherThanNameAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var owned = Path.Combine(fixture.PreparedRootPath, "ordinary-spelling");
        Directory.CreateDirectory(owned);
        await File.WriteAllTextAsync(Path.Combine(owned, "private.txt"), "tool-state bytes");
        var (rootStatus, rootHandle) = PreparedSourceTraversal.OpenRoot(fixture.PreparedRootPath);
        using var root = rootHandle;
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, rootStatus);
        Assert.NotNull(root);
        var (directoryStatus, directoryHandle) = PreparedSourceTraversal.OpenRelativeDirectory(root, "ordinary-spelling");
        using var directory = directoryHandle;
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, directoryStatus);
        Assert.NotNull(directory);
        var identity = PreparedSourceTraversal.ObserveDirectory(directory);
        Assert.NotNull(identity);
        var (status, opened) = PreparedSourceTraversal.OpenRelativeFile(root, "ordinary-spelling/private.txt", excludedDirectory: identity);
        using var unexpectedlyOpened = opened;
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Denied, status);
        Assert.Null(opened);
        var (childStatus, childHandle) = PreparedSourceTraversal.OpenRelativeDirectory(root, "ordinary-spelling", excludedDirectory: identity);
        using var unexpectedDirectory = childHandle;
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Denied, childStatus);
        Assert.Null(childHandle);
        var (ordinaryStatus, ordinaryHandle) = PreparedSourceTraversal.OpenRelativeFile(root, "notes.oddlangx", excludedDirectory: identity);
        using var ordinary = ordinaryHandle;
        Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, ordinaryStatus);
        Assert.NotNull(ordinary);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ObserveFileIdentityAsync_RewriteOrAuthorityLoss_NeverReusesOldObservationAsync()
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        await using var context = await IssueAsync(fixture);
        var first = await issuer.ObserveFileIdentityAsync(context, "notes.oddlangx");
        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Equal(first, await issuer.ObserveFileIdentityAsync(context, "notes.oddlangx"));
        await File.WriteAllTextAsync(fixture.UnfamiliarTextPath, "replaced source content");
        File.SetLastWriteTimeUtc(fixture.UnfamiliarTextPath, DateTime.UtcNow.AddSeconds(1));
        var changed = await issuer.ObserveFileIdentityAsync(context, "notes.oddlangx");
        Assert.True(changed is null || changed != first, "A changed revision cannot reuse the old identity.");
        fixture.Authority.Revoke();
        Assert.Null(await issuer.ObserveFileIdentityAsync(context, "notes.oddlangx"));
        Assert.Null(await issuer.ObserveFileIdentityAsync(context, "../outside/sentinel.txt"));
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObserveFileIdentityAsync_ListOrSearchThenRewrite_CannotBlessNewRevisionForOldDiscoveryAsync(bool search)
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var directory = Path.Combine(fixture.PreparedRootPath, "scan");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "changing.txt");
        await File.WriteAllTextAsync(path, "AAAA BBBB");
        await using var context = await IssueAsync(fixture);
        if (search)
        {
            var result = await context.SearchTextAsync(new("scan", "AAAA", 10));
            Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
            Assert.Equal("scan/changing.txt", Assert.Single(result.Matches).Location.RelativePath);
        }
        else
        {
            var result = await context.ListAsync(new("scan", 10));
            Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
            Assert.Equal("scan/changing.txt", Assert.Single(result.Entries).Location.RelativePath);
        }
        await File.WriteAllTextAsync(path, "CCCC DDDD");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(1));
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        Assert.Null(await issuer.ObserveFileIdentityAsync(context, "scan/changing.txt"));
        var range = await context.ReadTextRangeAsync(new("scan/changing.txt", 0, 4));
        Assert.Equal(PreparedSourceOutcome.ChangedInput, range.Outcome);
        Assert.Null(range.Text);
    }
    private static PreparedCodeGraphConnection CreateOwner(PreparedSourceFixture fixture, string root, Guid? repository = null) => new(
        repository ?? fixture.Approval.RepositoryId, Path.Combine(fixture.SandboxPath, "never-executed.ps1"),
        "never-executed", [], root, new Dictionary<string, string>(), static () => { });
    private static async Task<IPreparedSourceReadContext> IssueAsync(PreparedSourceFixture fixture)
    {
        Assert.True(OperatingSystem.IsWindows(), "Contained source fixtures require the authorized Windows host.");
        fixture.Authority.Approve(fixture.Approval);
        var result = await new PreparedSourceContextIssuer(fixture.Authority).IssueAsync(new(fixture.Approval, fixture.PreparedRootPath, PreparedSourceGrantLimits.Default));
        Assert.Equal(PreparedSourceIssueStatus.Issued, result.Status);
        return Assert.IsAssignableFrom<IPreparedSourceReadContext>(result.Context);
    }
}
