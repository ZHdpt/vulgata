using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceReadContextTests
{
    [Fact]
    public async Task OpenReadContextAsync_AfterCaptureReturnsBoundGrantAndImmediateMetadata()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/a.txt"] = Encoding.UTF8.GetBytes("alpha\n"),
                ["src/dir/b.txt"] = Encoding.UTF8.GetBytes("bravo\n"),
                ["src/binary.bin"] = new byte[] { 0x00, 0xFF, 0x01 },
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        string profiledRevision = await fixture.Repository.ResolveRevisionAsync(permittedRef);

        SourceCaptureResult capture = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);

        SourceReadGrantRequest grant = new(
            maximumOperations: 4,
            maximumMetadataEntries: 16,
            maximumReturnedBytes: 128,
            maximumEntriesPerOperation: 8,
            maximumBytesPerOperation: 64,
            deadline: TimeSpan.FromSeconds(5));
        OpenSourceReadContextResult opened = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .OpenReadContextAsync(fixture.RepositoryId, profiledRevision, grant);

        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.Equal(fixture.RepositoryId, opened.RepositoryId);
        Assert.Equal(profiledRevision, opened.ProfiledRevision);
        Assert.Equal(capture.ManifestSha256, opened.ManifestSha256);
        Assert.NotNull(opened.Context);
        await using ISourceReadContext context = opened.Context!;

        SourceEntryResult entry = await context.GetEntryAsync("src/a.txt");
        Assert.Equal(SourceReadStatus.Succeeded, entry.Status);
        Assert.Equal(fixture.RepositoryId, entry.RepositoryId);
        Assert.Equal(profiledRevision, entry.ProfiledRevision);
        Assert.Equal(capture.ManifestSha256, entry.ManifestSha256);
        Assert.Equal("src/a.txt", entry.Entry?.RelativePath);
        Assert.Equal(SourceEntryKind.RegularFile, entry.Entry?.Kind);
        Assert.Contains(SourceEntryCapability.StrictUtf8Text, entry.Entry?.Capabilities ?? []);

        SourceListResult firstPage = await context.ListAsync("src", limit: 2, cursor: null);
        Assert.Equal(SourceReadStatus.Succeeded, firstPage.Status);
        Assert.Equal(2, firstPage.Entries.Count);
        Assert.NotNull(firstPage.NextCursor);
        Assert.All(firstPage.Entries, value => Assert.StartsWith("src/", value.RelativePath, StringComparison.Ordinal));

        SourceListResult secondPage = await context.ListAsync("src", limit: 2, firstPage.NextCursor);
        Assert.Equal(SourceReadStatus.Succeeded, secondPage.Status);
        Assert.Single(secondPage.Entries);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task ReadContext_RequestedGrantOnlyReducesServerBudgetAndBindsCursorsToContext()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/a.txt"] = Encoding.UTF8.GetBytes("alpha\n"),
                ["src/b.txt"] = Encoding.UTF8.GetBytes("bravo\n"),
                ["src/dir/c.txt"] = Encoding.UTF8.GetBytes("charlie\n"),
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);

        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(
                maximumOperations: 8,
                maximumMetadataEntries: 1,
                maximumEntriesPerOperation: 2));
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        await using ISourceReadContext context = opened.Context!;

        SourceEntryResult first = await context.GetEntryAsync("src/a.txt");
        Assert.Equal(SourceReadStatus.Succeeded, first.Status);
        SourceEntryResult second = await context.GetEntryAsync("src/b.txt");
        Assert.Equal(SourceReadStatus.BudgetExceeded, second.Status);
        Assert.Equal("source_budget_metadata_entries_exceeded", second.Code);

        OpenSourceReadContextResult listContext = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(maximumEntriesPerOperation: 1));
        await using ISourceReadContext listReader = listContext.Context!;
        SourceListResult firstPage = await listReader.ListAsync("src", 1, null);
        Assert.Equal(SourceReadStatus.Succeeded, firstPage.Status);
        Assert.NotNull(firstPage.NextCursor);

        OpenSourceReadContextResult otherContext = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(maximumEntriesPerOperation: 2));
        await using ISourceReadContext otherReader = otherContext.Context!;
        SourceListResult foreignCursor = await otherReader.ListAsync("src", 1, firstPage.NextCursor);
        Assert.Equal(SourceReadStatus.Denied, foreignCursor.Status);

        SourceListResult tamperedCursor = await listReader.ListAsync(
            "src",
            1,
            firstPage.NextCursor + "tampered");
        Assert.Equal(SourceReadStatus.Denied, tamperedCursor.Status);
    }

    [Fact]
    public async Task OpenReadContextAsync_AfterAuthorityRevocation_DeniesFreshGrant()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        await RetainedSourceCaptureApplicationFixture.RevokeAuthorityAsync(
            appFixture.Database.ConnectionString,
            appFixture.SystemId,
            appFixture.UserId);

        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());

        Assert.Equal(SourceReadStatus.Denied, opened.Status);
        Assert.Equal("source_denied", opened.Code);
        Assert.Null(opened.Context);
    }

    [Fact]
    public async Task OpenReadContextAsync_WhenManifestIsCorrupt_ReturnsIntegrityFailureWithoutContext()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        string manifestPath = await ManifestPathAsync(fixture, appFixture);
        await File.WriteAllBytesAsync(manifestPath, [0x7B, 0x00, 0x7D]);

        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());

        Assert.Equal(SourceReadStatus.IntegrityFailure, opened.Status);
        Assert.Equal("source_integrity_failed", opened.Code);
        Assert.Null(opened.Context);
    }

    [Fact]
    public async Task ReadContext_AfterManifestMutation_RevalidatesBeforeReturningMetadata()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        await using ISourceReadContext context = opened.Context!;
        string manifestPath = await ManifestPathAsync(fixture, appFixture);
        await File.WriteAllBytesAsync(manifestPath, [0x7B, 0x00, 0x7D]);

        SourceEntryResult entry = await context.GetEntryAsync("src/retained-source.txt");

        Assert.Equal(SourceReadStatus.IntegrityFailure, entry.Status);
        Assert.Equal("source_integrity_failed", entry.Code);
        Assert.Null(entry.Entry);
    }

    [Fact]
    public async Task ReadContext_WhenCallerCancelsAfterMetadataAuthorization_ReturnsCancelled()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                mutateAfterFirstAuthorization: _ =>
                {
                    cancellation.Cancel();
                    return Task.CompletedTask;
                },
                mutateAfterAuthorizationCount: 6);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        await using ISourceReadContext context = opened.Context!;

        SourceEntryResult entry = await context.GetEntryAsync(
            "src/retained-source.txt",
            cancellation.Token);

        Assert.Equal(SourceReadStatus.Cancelled, entry.Status);
        Assert.Equal("source_cancelled", entry.Code);
        Assert.Null(entry.Entry);
    }

    [Fact]
    public async Task ReadContext_DisposedContextRejectsFurtherMetadataAttempts()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        ISourceReadContext context = opened.Context!;
        await context.DisposeAsync();

        SourceEntryResult entry = await context.GetEntryAsync("src/retained-source.txt");

        Assert.Equal(SourceReadStatus.Cancelled, entry.Status);
        Assert.Equal("source_cancelled", entry.Code);
        Assert.Null(entry.Entry);
    }

    [Fact]
    public async Task ReadContext_ExactOperationBudgetStopsTheSecondAttempt()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(maximumOperations: 1));
        await using ISourceReadContext context = opened.Context!;

        SourceEntryResult first = await context.GetEntryAsync("src/retained-source.txt");
        SourceEntryResult second = await context.GetEntryAsync("src/retained-source.txt");

        Assert.Equal(SourceReadStatus.Succeeded, first.Status);
        Assert.Equal(SourceReadStatus.BudgetExceeded, second.Status);
        Assert.Equal("source_budget_operations_exceeded", second.Code);
        Assert.Null(second.Entry);
    }

    [Fact]
    public async Task OpenReadContextAsync_NonPositiveRequestedLimitsAreRejected()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);

        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(maximumOperations: 0, maximumMetadataEntries: -1));

        if (opened.Context is null)
        {
            Assert.NotEqual(SourceReadStatus.Succeeded, opened.Status);
            return;
        }

        await using ISourceReadContext context = opened.Context;
        SourceEntryResult entry = await context.GetEntryAsync("src/retained-source.txt");
        Assert.NotEqual(SourceReadStatus.Succeeded, entry.Status);
        Assert.Null(entry.Entry);
    }

    [Fact]
    public async Task OpenReadContextAsync_RequestCannotRaiseConfiguredReadCaps()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/a.txt"] = Encoding.UTF8.GetBytes("alpha\n"),
                ["src/b.txt"] = Encoding.UTF8.GetBytes("bravo\n"),
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:Read:MaximumEntriesPerOperation"] = "1",
                    ["SourceStorage:Read:MaximumMetadataEntries"] = "1",
                });
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(
                maximumEntriesPerOperation: 100,
                maximumMetadataEntries: 100));
        await using ISourceReadContext context = opened.Context!;

        SourceListResult list = await context.ListAsync("src", 2, null);

        Assert.Equal(SourceReadStatus.BudgetExceeded, list.Status);
        Assert.Equal("source_budget_entries_per_operation_exceeded", list.Code);
        Assert.Equal(SourceBudgetDimension.EntriesPerOperation, list.ExhaustedDimension);
        Assert.Empty(list.Entries);
    }

    [Fact]
    public async Task OpenReadContextAsync_LoweredMonotonicDeadlineStopsMetadataAttempt()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:CaptureDeadlineSeconds"] = "100000",
                    ["SourceStorage:Read:DeadlineSeconds"] = "1",
                },
                timeProvider: new AdvancingRetainedSourceTimeProvider(TimeSpan.FromSeconds(2)));
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(deadline: TimeSpan.FromSeconds(1)));
        await using ISourceReadContext context = opened.Context!;

        SourceEntryResult entry = await context.GetEntryAsync("src/retained-source.txt");

        Assert.Equal(SourceReadStatus.DeadlineExceeded, entry.Status);
        Assert.Equal("source_deadline_exceeded", entry.Code);
        Assert.Null(entry.Entry);
    }

    [Fact]
    public async Task OpenReadContextAsync_MissingSourceAndEntryReturnSanitizedUnavailableResults()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);

        OpenSourceReadContextResult missing = await OpenAsync(
            fixture,
            appFixture,
            new string('0', revision.Length),
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.NotFoundOrUnavailable, missing.Status);
        Assert.Equal("source_not_found_or_unavailable", missing.Code);
        Assert.Null(missing.Context);

        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());
        await using ISourceReadContext context = opened.Context!;
        SourceEntryResult entry = await context.GetEntryAsync("src/missing.txt");
        Assert.Equal(SourceReadStatus.NotFoundOrUnavailable, entry.Status);
        Assert.Equal("source_not_found_or_unavailable", entry.Code);
        Assert.Null(entry.Entry);
    }

    [Fact]
    public async Task OpenReadContextAsync_MissingBlobReturnsIntegrityFailure()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        string blobPath = await FirstBlobPathAsync(fixture, appFixture);
        File.Delete(blobPath);

        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());

        Assert.Equal(SourceReadStatus.IntegrityFailure, opened.Status);
        Assert.Equal("source_integrity_failed", opened.Code);
        Assert.Null(opened.Context);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/absolute")]
    [InlineData("../escape")]
    [InlineData("src//file.txt")]
    [InlineData("src/./file.txt")]
    [InlineData("src/../file.txt")]
    [InlineData("src\\file.txt")]
    [InlineData("C:file.txt")]
    public async Task ReadContext_PathContainmentVariantsAreDenied(string path)
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());
        await using ISourceReadContext context = opened.Context!;

        SourceEntryResult entry = await context.GetEntryAsync(path);

        Assert.Equal(SourceReadStatus.Denied, entry.Status);
        Assert.Equal("source_denied", entry.Code);
        Assert.Null(entry.Entry);
    }

    [Fact]
    public async Task ReadContext_RootListingReportsUnsupportedRawPathLimitations()
    {
        byte[] unsafePath = Encoding.ASCII.GetBytes("unsafe\\name");
        byte[] invalidPath = [(byte)'b', 0xFF, (byte)'a', (byte)'d'];
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await fixture.Repository.CommitRawTreeAsync(
            [
                (invalidPath, Encoding.UTF8.GetBytes("invalid path content\n")),
                (unsafePath, Encoding.UTF8.GetBytes("unsafe path content\n")),
            ]);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());
        await using ISourceReadContext context = opened.Context!;

        SourceListResult list = await context.ListAsync(string.Empty, 10, null);

        Assert.Equal(SourceReadStatus.Succeeded, list.Status);
        Assert.Empty(list.Entries);
        Assert.Equal(2, list.Limitations.Count);
        Assert.All(list.Limitations, limitation =>
            Assert.Equal("source_path_unsupported", limitation.LimitationCode));
    }

    [Fact]
    public async Task ReadContext_RootListingPagesSafeAndUnsupportedMetadataWithinOneCumulativeBudget()
    {
        byte[] unsafePath = Encoding.ASCII.GetBytes("unsafe\\name");
        byte[] invalidPath = [(byte)'b', 0xFF, (byte)'a', (byte)'d'];
        byte[] safePath = Encoding.ASCII.GetBytes("safe.txt");
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await fixture.Repository.CommitRawTreeAsync(
            [
                (invalidPath, Encoding.UTF8.GetBytes("invalid path\n")),
                (safePath, Encoding.UTF8.GetBytes("safe path\n")),
                (unsafePath, Encoding.UTF8.GetBytes("unsafe path\n")),
            ]);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest(
                maximumOperations: 5,
                maximumMetadataEntries: 3,
                maximumEntriesPerOperation: 1));
        await using ISourceReadContext context = opened.Context!;

        var allEntries = new List<SourceEntry>();
        var allLimitations = new List<SourceLimitationSummary>();
        string? cursor = null;
        for (int pageNumber = 0; pageNumber < 3; pageNumber++)
        {
            SourceListResult page = await context.ListAsync(string.Empty, 1, cursor);
            Assert.Equal(SourceReadStatus.Succeeded, page.Status);
            Assert.Equal(1, page.Entries.Count + page.Limitations.Count);
            allEntries.AddRange(page.Entries);
            allLimitations.AddRange(page.Limitations);
            cursor = page.NextCursor;
            if (pageNumber < 2)
            {
                Assert.NotNull(cursor);
            }
            else
            {
                Assert.Null(cursor);
            }
        }

        SourceListResult exhausted = await context.ListAsync(string.Empty, 1, cursor);
        Assert.Equal(SourceReadStatus.BudgetExceeded, exhausted.Status);
        Assert.Equal(SourceBudgetDimension.MetadataEntries, exhausted.ExhaustedDimension);
        Assert.Single(allEntries);
        Assert.Equal("safe.txt", allEntries[0].RelativePath);
        Assert.Equal(2, allLimitations.Count);
        Assert.All(allLimitations, limitation =>
            Assert.Equal("source_path_unsupported", limitation.LimitationCode));
        Assert.Equal(3, allEntries.Count + allLimitations.Count);
        Assert.Equal(2, allLimitations.Select(value => value.Ordinal).Distinct().Count());
    }

    [Fact]
    public async Task OpenReadContextAsync_SameLengthBlobCorruptionReturnsIntegrityFailure()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string revision = await CaptureAsync(fixture, appFixture);
        string blobPath = await FirstBlobPathAsync(fixture, appFixture);
        byte[] original = await File.ReadAllBytesAsync(blobPath);
        Assert.NotEmpty(original);
        original[0] ^= 0xFF;
        await File.WriteAllBytesAsync(blobPath, original);

        OpenSourceReadContextResult opened = await OpenAsync(
            fixture,
            appFixture,
            revision,
            new SourceReadGrantRequest());

        Assert.Equal(SourceReadStatus.IntegrityFailure, opened.Status);
        Assert.Equal("source_integrity_failed", opened.Code);
        Assert.Null(opened.Context);
    }

    private static async Task<string> CaptureAsync(
        RetainedSourceFixture fixture,
        RetainedSourceCaptureApplicationFixture appFixture)
    {
        string revision = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, revision);
        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        return result.ProfiledRevision!;
    }

    private static async Task<OpenSourceReadContextResult> OpenAsync(
        RetainedSourceFixture fixture,
        RetainedSourceCaptureApplicationFixture appFixture,
        string revision,
        SourceReadGrantRequest grant) =>
        await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .OpenReadContextAsync(fixture.RepositoryId, revision, grant);

    private static async Task<string> ManifestPathAsync(
        RetainedSourceFixture fixture,
        RetainedSourceCaptureApplicationFixture appFixture)
    {
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots
            .AsNoTracking()
            .SingleAsync(value => value.RepositoryId == fixture.RepositoryId);
        return Path.Combine(
            new[] { fixture.SourceStorageRoot }
                .Concat(snapshot.InternalStorageKey.Split('/'))
                .Append("manifest.json")
                .ToArray());
    }

    private static async Task<string> FirstBlobPathAsync(
        RetainedSourceFixture fixture,
        RetainedSourceCaptureApplicationFixture appFixture)
    {
        string manifestPath = await ManifestPathAsync(fixture, appFixture);
        string blobsRoot = Path.Combine(Directory.GetParent(manifestPath)!.FullName, "blobs");
        return Directory.EnumerateFiles(blobsRoot).Single();
    }
}
