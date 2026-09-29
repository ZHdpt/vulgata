using Vulgata.Tests.TestInfrastructure;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceRangeReadTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_ReturnsExactByteRangeAndStrictUtf8TextReceipts()
    {
        byte[] content = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("héllo\r\n世界\n"))
            .ToArray();
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/unicode.txt"] = content,
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        SourceCaptureResult capture = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);

        fixture.Repository.WriteText("src/unicode.txt", "mutated working copy\n");
        string changedRevision = await fixture.Repository.CommitAsync("mutate after capture");
        Assert.NotEqual(capture.ProfiledRevision, changedRevision);

        OpenSourceReadContextResult opened = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .OpenReadContextAsync(
                fixture.RepositoryId,
                capture.ProfiledRevision!,
                new SourceReadGrantRequest(
                    maximumOperations: 4,
                    maximumMetadataEntries: 4,
                    maximumReturnedBytes: content.Length * 2,
                    maximumEntriesPerOperation: 2,
                    maximumBytesPerOperation: content.Length));
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        await using ISourceReadContext context = opened.Context!;

        const int offset = 3;
        byte[] expectedBytes = content[offset..];
        SourceBytesResult bytes = await context.ReadBytesAsync(
            "src/unicode.txt",
            offset,
            expectedBytes.Length);

        Assert.Equal(SourceReadStatus.Succeeded, bytes.Status);
        Assert.Equal("source_bytes_succeeded", bytes.Code);
        Assert.Equal(fixture.RepositoryId, bytes.RepositoryId);
        Assert.Equal(capture.ProfiledRevision, bytes.ProfiledRevision);
        Assert.Equal(capture.ManifestSha256, bytes.ManifestSha256);
        Assert.Equal("src/unicode.txt", bytes.RelativePath);
        Assert.Equal(offset, bytes.ByteOffset);
        Assert.Equal(expectedBytes.Length, bytes.ByteLength);
        Assert.Equal(content.Length, bytes.EntryByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), bytes.EntrySha256);
        Assert.Equal(expectedBytes, bytes.Content.ToArray());

        SourceTextResult text = await context.ReadTextAsync(
            "src/unicode.txt",
            0,
            content.Length);

        Assert.Equal(SourceReadStatus.Succeeded, text.Status);
        Assert.Equal("source_text_succeeded", text.Code);
        Assert.Equal("src/unicode.txt", text.RelativePath);
        Assert.Equal(0, text.ByteOffset);
        Assert.Equal(content.Length, text.ByteLength);
        Assert.Equal(content.Length, text.EntryByteLength);
        Assert.Equal(Encoding.UTF8.GetString(content), text.Content);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), text.EntrySha256);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_EnforcesEofAndInvalidRangeBoundaries()
    {
        byte[] content = Encoding.UTF8.GetBytes("abc");
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/range.txt"] = content,
                ["src/empty.txt"] = ReadOnlyMemory<byte>.Empty,
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(maximumOperations: 10, maximumReturnedBytes: 32));

        SourceBytesResult exact = await context.ReadBytesAsync("src/range.txt", 0, content.Length);
        SourceBytesResult eof = await context.ReadBytesAsync("src/range.txt", content.Length, 1);
        SourceBytesResult pastEof = await context.ReadBytesAsync("src/range.txt", content.Length + 1, 1);
        SourceBytesResult negative = await context.ReadBytesAsync("src/range.txt", -1, 1);
        SourceTextResult textEof = await context.ReadTextAsync("src/range.txt", content.Length, 1);
        SourceBytesResult empty = await context.ReadBytesAsync("src/empty.txt", 0, 1);
        SourceTextResult emptyText = await context.ReadTextAsync("src/empty.txt", 0, 1);

        Assert.Equal(SourceReadStatus.Succeeded, exact.Status);
        Assert.Equal(content, exact.Content.ToArray());
        Assert.Equal(SourceReadStatus.Succeeded, eof.Status);
        Assert.Equal("source_bytes_succeeded", eof.Code);
        Assert.Empty(eof.Content.ToArray());
        Assert.Equal(content.Length, eof.ByteOffset);
        Assert.Equal(0, eof.ByteLength);
        Assert.Equal(content.Length, eof.EntryByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), eof.EntrySha256);
        Assert.Equal(SourceReadStatus.InvalidRange, pastEof.Status);
        Assert.Equal(SourceReadStatus.Denied, negative.Status);
        Assert.Equal(SourceReadStatus.Succeeded, textEof.Status);
        Assert.Equal("source_text_succeeded", textEof.Code);
        Assert.Equal(string.Empty, textEof.Content);
        Assert.Equal(content.Length, textEof.ByteOffset);
        Assert.Equal(0, textEof.ByteLength);
        Assert.Equal(content.Length, textEof.EntryByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), textEof.EntrySha256);
        Assert.Equal(SourceReadStatus.Succeeded, empty.Status);
        Assert.Equal("src/empty.txt", empty.RelativePath);
        Assert.Empty(empty.Content.ToArray());
        Assert.Equal(0, empty.ByteOffset);
        Assert.Equal(0, empty.ByteLength);
        Assert.Equal(0, empty.EntryByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData([])), empty.EntrySha256);
        Assert.Equal(SourceReadStatus.Succeeded, emptyText.Status);
        Assert.Equal(string.Empty, emptyText.Content);
        Assert.Equal(0, emptyText.ByteOffset);
        Assert.Equal(0, emptyText.ByteLength);
        Assert.Equal(0, emptyText.EntryByteLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData([])), emptyText.EntrySha256);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_EmptyFileConsumesReservedMaximumEvenWhenPayloadIsEmpty()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/empty.txt"] = ReadOnlyMemory<byte>.Empty,
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(
                maximumOperations: 4,
                maximumReturnedBytes: 1,
                maximumBytesPerOperation: 1));

        SourceBytesResult first = await context.ReadBytesAsync("src/empty.txt", 0, 1);
        SourceTextResult second = await context.ReadTextAsync("src/empty.txt", 0, 1);

        Assert.Equal(SourceReadStatus.Succeeded, first.Status);
        Assert.Empty(first.Content.ToArray());
        Assert.Equal(SourceReadStatus.BudgetExceeded, second.Status);
        Assert.Equal(SourceBudgetDimension.ReturnedBytes, second.ExhaustedDimension);
        Assert.Null(second.Content);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_StrictTextRejectsScalarSplitAndUnsupportedEncoding()
    {
        byte[] scalarContent = Encoding.UTF8.GetBytes("A😀B");
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/scalar.txt"] = scalarContent,
                ["src/invalid.bin"] = new byte[] { 0xFF, 0xFE },
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(maximumOperations: 10, maximumReturnedBytes: 64));

        SourceTextResult split = await context.ReadTextAsync("src/scalar.txt", 2, 4);
        SourceTextResult invalid = await context.ReadTextAsync("src/invalid.bin", 0, 2);

        Assert.Equal(SourceReadStatus.InvalidRange, split.Status);
        Assert.Equal("source_invalid_range", split.Code);
        Assert.Equal(SourceReadStatus.Unsupported, invalid.Status);
        Assert.Equal("source_unsupported", invalid.Code);
        Assert.Equal("source_text_unsupported", invalid.LimitationCode);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_UnsupportedSymlinkAndGitlinkHaveNoBytePayload()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await fixture.Repository.AddSymbolicLinkEntryAsync("link", "target.txt");
        await fixture.Repository.AddGitlinkEntryAsync("submodule");
        await fixture.Repository.CommitIndexAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(maximumOperations: 10, maximumReturnedBytes: 64));

        SourceBytesResult symlink = await context.ReadBytesAsync("link", 0, 8);
        SourceTextResult gitlink = await context.ReadTextAsync("submodule", 0, 8);
        SourceBytesResult directory = await context.ReadBytesAsync("src", 0, 8);

        Assert.Equal(SourceReadStatus.Unsupported, symlink.Status);
        Assert.Equal("source_symbolic_link_unsupported", symlink.LimitationCode);
        Assert.Equal(SourceReadStatus.Unsupported, gitlink.Status);
        Assert.Equal("source_gitlink_unsupported", gitlink.LimitationCode);
        Assert.Equal(SourceReadStatus.NotFoundOrUnavailable, directory.Status);
        Assert.Empty(symlink.Content.ToArray());
        Assert.Null(gitlink.Content);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_ReturnedByteBudgetIsCumulativeWithoutRefund()
    {
        byte[] content = Encoding.UTF8.GetBytes("returned bytes");
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/budget.txt"] = content,
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(
                maximumOperations: 4,
                maximumReturnedBytes: content.Length,
                maximumBytesPerOperation: content.Length));

        SourceBytesResult first = await context.ReadBytesAsync("src/budget.txt", 0, content.Length);
        SourceBytesResult second = await context.ReadBytesAsync("src/budget.txt", 0, content.Length);

        Assert.Equal(SourceReadStatus.Succeeded, first.Status);
        Assert.Equal(SourceReadStatus.BudgetExceeded, second.Status);
        Assert.Equal(SourceBudgetDimension.ReturnedBytes, second.ExhaustedDimension);
        Assert.Empty(second.Content.ToArray());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_ConcurrentReservationsAdmitOnlyWithinOperationAndByteBudgets()
    {
        byte[] content = Encoding.UTF8.GetBytes("concurrent bytes");
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/concurrent.txt"] = content,
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(
                maximumOperations: 4,
                maximumReturnedBytes: content.Length * 2,
                maximumBytesPerOperation: content.Length));

        SourceBytesResult[] results = await Task.WhenAll(
            Enumerable.Range(0, 4)
                .Select(_ => context.ReadBytesAsync("src/concurrent.txt", 0, content.Length)));

        Assert.Equal(2, results.Count(result => result.Status == SourceReadStatus.Succeeded));
        Assert.Equal(2, results.Count(result => result.Status == SourceReadStatus.BudgetExceeded));
        Assert.All(
            results.Where(result => result.Status == SourceReadStatus.Succeeded),
            result => Assert.Equal(content, result.Content.ToArray()));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_CooperativeAuthorizationWaitStopsAtReadDeadlineBeforeWatchdog()
    {
        var time = new ControlledRetainedSourceTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int readAuthorizationCount = 0;
        int waitCancelled = 0;
        using var callerCancellation = new CancellationTokenSource();
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:CaptureDeadlineSeconds"] = "100000",
                    ["SourceStorage:Read:DeadlineSeconds"] = "1",
                },
                timeProvider: time,
                authorizationWait: async (request, cancellationToken) =>
                {
                    if (request.Purpose != SourceAuthorizationPurpose.Read ||
                        Interlocked.Increment(ref readAuthorizationCount) != 3)
                    {
                        return;
                    }

                    entered.TrySetResult();
                    time.Advance(TimeSpan.FromSeconds(2));
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Exchange(ref waitCancelled, 1);
                        throw;
                    }
                });
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(
                maximumOperations: 4,
                maximumReturnedBytes: 64,
                maximumBytesPerOperation: 32,
                deadline: TimeSpan.FromSeconds(1)));

        Task<SourceBytesResult> operation = context.ReadBytesAsync(
            "src/retained-source.txt",
            0,
            4,
            callerCancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task watchdog = Task.Delay(TimeSpan.FromSeconds(10));
            Task completed = await Task.WhenAny(operation, watchdog);
            if (completed != operation)
            {
                await callerCancellation.CancelAsync();
            }
            SourceBytesResult result = await operation;

            Assert.False(
                completed != operation,
                $"The watchdog won; the operation returned {result.Status}/{result.Code} after caller cancellation.");
            Assert.True(readAuthorizationCount >= 3);
            Assert.Equal(1, waitCancelled);
            Assert.Equal(SourceReadStatus.DeadlineExceeded, result.Status);
            Assert.Equal("source_deadline_exceeded", result.Code);
            Assert.Empty(result.Content.ToArray());
        }
        finally
        {
            await callerCancellation.CancelAsync();
            try
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
        }
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_CallerCancellationAfterRangeAuthorizationReturnsNoPayload()
    {
        using var cancellation = new CancellationTokenSource();
        int callbackCount = 0;
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                mutateAfterFirstAuthorization: _ =>
                {
                    Interlocked.Increment(ref callbackCount);
                    cancellation.Cancel();
                    return Task.CompletedTask;
                },
                mutateAfterAuthorizationCount: 7);
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(maximumOperations: 8, maximumReturnedBytes: 64));

        SourceBytesResult result = await context.ReadBytesAsync(
            "src/retained-source.txt",
            0,
            4,
            cancellation.Token);

        Assert.Equal(1, callbackCount);
        Assert.Equal(SourceReadStatus.Cancelled, result.Status);
        Assert.Equal("source_cancelled", result.Code);
        Assert.Empty(result.Content.ToArray());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_SameLengthBlobMutationOutsideSelectedRangeFailsIntegrity()
    {
        byte[] content = Enumerable.Repeat((byte)'x', 4_096).ToArray();
        int callbackCount = 0;
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/large.bin"] = content,
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                mutateAfterFirstAuthorization: _ =>
                {
                    Interlocked.Increment(ref callbackCount);
                    return MutateFirstBlobAsync(fixture.SourceStorageRoot);
                },
                mutateAfterAuthorizationCount: 6);
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(maximumOperations: 8, maximumReturnedBytes: 64));

        SourceBytesResult result = await context.ReadBytesAsync("src/large.bin", 0, 4);

        Assert.Equal(1, callbackCount);
        Assert.Equal(SourceReadStatus.IntegrityFailure, result.Status);
        Assert.Equal("source_integrity_failed", result.Code);
        Assert.Empty(result.Content.ToArray());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_PerOperationByteCapRejectsByteAndTextRequestsWithoutPayload()
    {
        byte[] content = Encoding.UTF8.GetBytes("abcdefgh");
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/cap.txt"] = content,
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:Read:MaximumBytesPerOperation"] = "4",
                    ["SourceStorage:Read:MaximumReturnedBytes"] = "32",
                });
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(
                maximumOperations: 8,
                maximumReturnedBytes: 32,
                maximumBytesPerOperation: 16));

        SourceBytesResult bytes = await context.ReadBytesAsync("src/cap.txt", 0, 8);
        SourceTextResult text = await context.ReadTextAsync("src/cap.txt", 0, 8);

        Assert.Equal(SourceReadStatus.BudgetExceeded, bytes.Status);
        Assert.Equal("source_budget_bytes_per_operation_exceeded", bytes.Code);
        Assert.Equal(SourceBudgetDimension.BytesPerOperation, bytes.ExhaustedDimension);
        Assert.Empty(bytes.Content.ToArray());
        Assert.Equal(SourceReadStatus.BudgetExceeded, text.Status);
        Assert.Equal("source_budget_bytes_per_operation_exceeded", text.Code);
        Assert.Equal(SourceBudgetDimension.BytesPerOperation, text.ExhaustedDimension);
        Assert.Null(text.Content);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadContext_ValidNinetyDayGrantHasNoTimerRangeFailure()
    {
        byte[] content = Encoding.UTF8.GetBytes("long grant\n");
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["src/long-grant.txt"] = content,
            });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:Read:DeadlineSeconds"] = "7776000",
                },
                timeProvider: new ControlledRetainedSourceTimeProvider());
        await using ISourceReadContext context = await CaptureAndOpenAsync(
            fixture,
            appFixture,
            new SourceReadGrantRequest(
                maximumOperations: 4,
                maximumReturnedBytes: 128,
                maximumBytesPerOperation: 64,
                deadline: TimeSpan.FromDays(90)));

        SourceBytesResult result = await context.ReadBytesAsync(
            "src/long-grant.txt",
            0,
            content.Length);

        Assert.Equal(SourceReadStatus.Succeeded, result.Status);
        Assert.Equal(content, result.Content.ToArray());
        Assert.Equal(content.Length, result.ByteLength);
    }

    private static async Task<ISourceReadContext> CaptureAndOpenAsync(
        RetainedSourceFixture fixture,
        RetainedSourceCaptureApplicationFixture appFixture,
        SourceReadGrantRequest grant)
    {
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        SourceCaptureResult capture = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        OpenSourceReadContextResult opened = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .OpenReadContextAsync(fixture.RepositoryId, capture.ProfiledRevision!, grant);
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        return opened.Context!;
    }

    private static async Task MutateFirstBlobAsync(string sourceStorageRoot)
    {
        string blobPath = Directory.EnumerateFiles(
                sourceStorageRoot,
                "*",
                SearchOption.AllDirectories)
            .Single(path => path.Contains("\\blobs\\", StringComparison.OrdinalIgnoreCase));
        byte[] bytes = await File.ReadAllBytesAsync(blobPath);
        Assert.True(bytes.Length > 1);
        bytes[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(blobPath, bytes);
    }
}
