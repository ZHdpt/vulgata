using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedSourceCancellationBudgetTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_GrantDeadlineExpiresAfterIssuance_ReturnsDeadlineExceededWithoutPayload()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantLimits limits = PreparedSourceGrantLimits.Default with
        {
            Deadline = TimeSpan.FromMilliseconds(250),
        };
        await using IPreparedSourceReadContext context = await IssueContextAsync(
            fixture,
            fixture.Authority,
            limits);
        await Task.Delay(TimeSpan.FromMilliseconds(750));

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", 0, 5));

        Assert.Equal(PreparedSourceOutcome.DeadlineExceeded, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.ExpiredOrCancelled, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchTextAsync_CancelledBetweenChunksDiscardsMatchAndSettlesFileHandle()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        const string relativeDirectory = "cancel-search";
        string searchRoot = Path.Combine(fixture.PreparedRootPath, relativeDirectory);
        Directory.CreateDirectory(searchRoot);
        byte[] content = Enumerable.Repeat((byte)'a', 8_192).ToArray();
        Array.Copy("needle"u8.ToArray(), content, 6);
        string physicalPath = Path.Combine(searchRoot, "large.oddx");
        await File.WriteAllBytesAsync(physicalPath, content);

        var authority = new PausingPreparedSourceAuthority(fixture.Approval, pauseAtVerification: 4);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        using var cancellation = new CancellationTokenSource();
        Task<PreparedSourceResult> pending = context.SearchTextAsync(
            new PreparedSourceSearchTextRequest(relativeDirectory, "needle", PageSize: 64),
            cancellation.Token);

        try
        {
            await authority.PausedVerificationEntered.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            cancellation.Cancel();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            throw;
        }

        cancellation.Cancel();
        PreparedSourceResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(PreparedSourceOutcome.Cancelled, result.Outcome);
        Assert.Empty(result.Matches);
        Assert.Null(result.Continuation);

        // The completed call must have closed its source handle. A no-share open
        // is a direct OS-level probe; the context's held root handle is unrelated.
        using var exclusive = new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_CancelledBetweenEntriesDiscardsBufferedMetadata()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        var authority = new PausingPreparedSourceAuthority(fixture.Approval, pauseAtVerification: 4);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        using var cancellation = new CancellationTokenSource();
        Task<PreparedSourceResult> pending = context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 100),
            cancellation.Token);

        try
        {
            await authority.PausedVerificationEntered.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            cancellation.Cancel();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            throw;
        }

        cancellation.Cancel();
        PreparedSourceResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(PreparedSourceOutcome.Cancelled, result.Outcome);
        Assert.Empty(result.Entries);
        Assert.Empty(result.Limitations);
        Assert.Null(result.Continuation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_CancelledBeforeReleaseDiscardsTextAndSettlesFileHandle()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        var authority = new PausingPreparedSourceAuthority(fixture.Approval, pauseAtVerification: 3);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        using var cancellation = new CancellationTokenSource();
        Task<PreparedSourceResult> pending = context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", 0, 5),
            cancellation.Token);

        try
        {
            await authority.PausedVerificationEntered.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            cancellation.Cancel();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            throw;
        }

        cancellation.Cancel();
        PreparedSourceResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(PreparedSourceOutcome.Cancelled, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.Continuation);
        using var exclusive = new FileStream(
            fixture.UnfamiliarTextPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_DisposedWhileAwaitingFinalAuthoritySettlesFileAndRootHandles()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        var authority = new GatePreparedSourceAuthority(fixture.Approval, pauseAtVerification: 3);
        IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        Task<PreparedSourceResult> pending = context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", 0, 5));

        try
        {
            await authority.PausedVerificationEntered.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            await context.DisposeAsync();
            authority.ReleasePausedVerification();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            throw;
        }

        await context.DisposeAsync();
        authority.ReleasePausedVerification();
        PreparedSourceResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(PreparedSourceOutcome.Cancelled, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
        using var exclusiveFile = new FileStream(
            fixture.UnfamiliarTextPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
        using SafeFileHandle exclusiveRoot = OpenExclusiveDirectoryOrAssert(fixture.PreparedRootPath);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_QueuedBeforeContextDisposal_ReturnsCancelledWithoutSourceFailure()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        var authority = new GatePreparedSourceAuthority(fixture.Approval, pauseAtVerification: 3);
        IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        Task<PreparedSourceResult> active = context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 100));
        Task<PreparedSourceResult>? queued = null;
        bool contextDisposed = false;

        try
        {
            await authority.PausedVerificationEntered.WaitAsync(TimeSpan.FromSeconds(5));

            // The second verifier check returns synchronously, while the first
            // call still owns the list gate. Its incomplete public task therefore
            // proves it has passed authorization and queued on that operation gate.
            queued = context.ListAsync(new PreparedSourceListRequest("", PageSize: 100));
            Assert.False(queued.IsCompleted, "The second list call should be waiting behind the active list call.");

            await context.DisposeAsync();
            contextDisposed = true;
        }
        finally
        {
            authority.ReleasePausedVerification();
            if (!contextDisposed)
            {
                await context.DisposeAsync();
            }

            await active.WaitAsync(TimeSpan.FromSeconds(5));
            if (queued is not null)
            {
                await queued.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        PreparedSourceResult activeResult = await active;
        PreparedSourceResult queuedResult = await queued!;

        Assert.Equal(PreparedSourceOutcome.Cancelled, activeResult.Outcome);
        Assert.Empty(activeResult.Entries);
        Assert.Empty(activeResult.Limitations);
        Assert.Null(activeResult.Continuation);
        Assert.Equal(PreparedSourceOutcome.Cancelled, queuedResult.Outcome);
        Assert.Empty(queuedResult.Entries);
        Assert.Empty(queuedResult.Limitations);
        Assert.Null(queuedResult.Continuation);
        Assert.Null(queuedResult.StoppingLimit);
        Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchTextAsync_QueuedBeforeContextDisposal_ReturnsCancelledWithoutSourceFailure()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        const string relativeDirectory = "queued-search";
        string searchRoot = Path.Combine(fixture.PreparedRootPath, relativeDirectory);
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(Path.Combine(searchRoot, "match.oddx"), "needle");

        var authority = new GatePreparedSourceAuthority(fixture.Approval, pauseAtVerification: 3);
        IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        Task<PreparedSourceResult> active = context.SearchTextAsync(
            new PreparedSourceSearchTextRequest(relativeDirectory, "needle", PageSize: 64));
        Task<PreparedSourceResult>? queued = null;
        bool contextDisposed = false;

        try
        {
            await authority.PausedVerificationEntered.WaitAsync(TimeSpan.FromSeconds(5));

            // The active search has reached its first file chunk while holding
            // the shared operation gate. An incomplete second task proves the
            // queued request has passed authorization but cannot yet scan.
            queued = context.SearchTextAsync(
                new PreparedSourceSearchTextRequest(relativeDirectory, "needle", PageSize: 64));
            Assert.False(queued.IsCompleted, "The second search call should wait behind the active search call.");

            await context.DisposeAsync();
            contextDisposed = true;
        }
        finally
        {
            authority.ReleasePausedVerification();
            if (!contextDisposed)
            {
                await context.DisposeAsync();
            }

            await active.WaitAsync(TimeSpan.FromSeconds(5));
            if (queued is not null)
            {
                await queued.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        PreparedSourceResult activeResult = await active;
        PreparedSourceResult queuedResult = await queued!;

        Assert.Equal(PreparedSourceOutcome.Cancelled, activeResult.Outcome);
        Assert.Empty(activeResult.Matches);
        Assert.Null(activeResult.Continuation);
        Assert.Equal(PreparedSourceOutcome.Cancelled, queuedResult.Outcome);
        Assert.Empty(queuedResult.Matches);
        Assert.Null(queuedResult.Continuation);
        Assert.Empty(queuedResult.Limitations);
        Assert.Null(queuedResult.StoppingLimit);
        Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ConcurrentReadCalls_ReserveOperationAndReturnedByteBudgetsAtomically()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string filePath = Path.Combine(fixture.PreparedRootPath, "one-byte.oddx");
        await File.WriteAllTextAsync(filePath, "x");
        var authority = new RendezvousPreparedSourceAuthority(fixture.Approval, arrivals: 2);
        PreparedSourceGrantLimits limits = PreparedSourceGrantLimits.Default with
        {
            MaximumOperations = 1,
            MaximumReturnedBytes = 2,
        };
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority, limits);
        var request = new PreparedSourceReadTextRangeRequest("one-byte.oddx", 0, 1);

        Task<PreparedSourceResult>[] pending =
        [
            context.ReadTextRangeAsync(request),
            context.ReadTextRangeAsync(request),
        ];
        try
        {
            await authority.AllCallsArrived.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            authority.ReleaseCalls();
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
            throw;
        }

        authority.ReleaseCalls();
        PreparedSourceResult[] results = await Task.WhenAll(pending);

        Assert.Single(results.Where(result => result.Outcome == PreparedSourceOutcome.Complete));
        PreparedSourceResult limited = Assert.Single(
            results.Where(result => result.Outcome == PreparedSourceOutcome.Limited));
        Assert.Equal(PreparedSourceStoppingLimit.GrantOperations, limited.StoppingLimit);
        Assert.Null(limited.Text);
        Assert.Null(limited.ReadLocation);
        Assert.Equal(1, results.Sum(result => result.Text?.Length ?? 0));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ConcurrentReadCalls_ReserveReturnedBytesAtomically()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string filePath = Path.Combine(fixture.PreparedRootPath, "one-byte.oddx");
        await File.WriteAllTextAsync(filePath, "x");
        var authority = new RendezvousPreparedSourceAuthority(fixture.Approval, arrivals: 2);
        PreparedSourceGrantLimits limits = PreparedSourceGrantLimits.Default with
        {
            MaximumOperations = 10,
            MaximumReturnedBytes = 1,
        };
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority, limits);
        var request = new PreparedSourceReadTextRangeRequest("one-byte.oddx", 0, 1);

        Task<PreparedSourceResult>[] pending =
        [
            context.ReadTextRangeAsync(request),
            context.ReadTextRangeAsync(request),
        ];
        try
        {
            await authority.AllCallsArrived.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            authority.ReleaseCalls();
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
            throw;
        }

        authority.ReleaseCalls();
        PreparedSourceResult[] results = await Task.WhenAll(pending);

        Assert.Single(results.Where(result => result.Outcome == PreparedSourceOutcome.Complete));
        PreparedSourceResult limited = Assert.Single(
            results.Where(result => result.Outcome == PreparedSourceOutcome.Limited));
        Assert.Equal(PreparedSourceStoppingLimit.GrantReturnedBytes, limited.StoppingLimit);
        Assert.Equal(1, results.Sum(result => result.Text?.Length ?? 0));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_DefaultGrantAllowsExactlyOneHundredOperations()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        var request = new PreparedSourceReadTextRangeRequest("notes.oddlangx", 0, 1);

        PreparedSourceResult[] allowed = new PreparedSourceResult[100];
        for (int index = 0; index < allowed.Length; index++)
        {
            allowed[index] = await context.ReadTextRangeAsync(request);
        }

        PreparedSourceResult exhausted = await context.ReadTextRangeAsync(request);

        Assert.All(allowed, result => Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome));
        Assert.Equal(PreparedSourceOutcome.Limited, exhausted.Outcome);
        Assert.Equal(PreparedSourceStoppingLimit.GrantOperations, exhausted.StoppingLimit);
        Assert.Null(exhausted.Text);
        Assert.Null(exhausted.ReadLocation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_DefaultGrantLimitsMetadataToOneThousandEntries()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        for (int index = 0; index < 1_001; index++)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(fixture.PreparedRootPath, $"metadata-{index:D4}.oddx"),
                []);
        }
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 100));

        Assert.Equal(PreparedSourceOutcome.Limited, result.Outcome);
        Assert.Equal(PreparedSourceStoppingLimit.GrantMetadataEntries, result.StoppingLimit);
        Assert.Null(result.Continuation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_DefaultGrantLimitsReturnedTextToOneMebibyte()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        const int grantMaximum = 1024 * 1024;
        string physicalPath = Path.Combine(fixture.PreparedRootPath, "returned-cap.oddx");
        await File.WriteAllBytesAsync(
            physicalPath,
            Enumerable.Repeat((byte)'R', grantMaximum + 1).ToArray());
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        long nextOffset = 0;

        for (int call = 0; call < grantMaximum / PreparedSourceCallLimits.MaximumReadBytes; call++)
        {
            PreparedSourceResult page = await context.ReadTextRangeAsync(
                new PreparedSourceReadTextRangeRequest(
                    "returned-cap.oddx",
                    nextOffset,
                    PreparedSourceCallLimits.MaximumReadBytes));
            Assert.Equal(PreparedSourceOutcome.Complete, page.Outcome);
            nextOffset = page.ReadLocation!.ByteEndExclusive;
        }

        PreparedSourceResult exhausted = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("returned-cap.oddx", nextOffset, 1));

        Assert.Equal(grantMaximum, nextOffset);
        Assert.Equal(PreparedSourceOutcome.Limited, exhausted.Outcome);
        Assert.Equal(PreparedSourceStoppingLimit.GrantReturnedBytes, exhausted.StoppingLimit);
        Assert.Null(exhausted.Text);
        Assert.Null(exhausted.NextByteOffset);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchTextAsync_DefaultGrantLimitsExaminedBytesToEightMebibytes()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        const int grantMaximum = 8 * 1024 * 1024;
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "examined-cap");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllBytesAsync(
            Path.Combine(searchRoot, "large.oddx"),
            Enumerable.Repeat((byte)'a', grantMaximum + 1).ToArray());
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        string? continuation = null;

        for (int call = 0; call < 8; call++)
        {
            PreparedSourceResult result = await context.SearchTextAsync(
                new PreparedSourceSearchTextRequest("examined-cap", "needle", PageSize: 64, continuation));
            Assert.Equal(PreparedSourceOutcome.Limited, result.Outcome);
            Assert.Equal(
                call == 7
                    ? PreparedSourceStoppingLimit.GrantExaminedBytes
                    : PreparedSourceStoppingLimit.CallExaminedBytes,
                result.StoppingLimit);
            Assert.Empty(result.Matches);
            continuation = result.Continuation;
        }

        Assert.Null(continuation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PreparedSourceGrant_DefaultLimitsRemainAtAcceptedApplicationMaxima()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantLimits maximum = PreparedSourceGrantLimits.Default;
        Assert.Equal(100, maximum.MaximumOperations);
        Assert.Equal(1_000, maximum.MaximumMetadataEntries);
        Assert.Equal(8 * 1024 * 1024, maximum.MaximumExaminedBytes);
        Assert.Equal(1024 * 1024, maximum.MaximumReturnedBytes);
        Assert.Equal(TimeSpan.FromSeconds(30), maximum.Deadline);
        Assert.Equal(100, PreparedSourceCallLimits.MaximumListEntries);
        Assert.Equal(64 * 1024, PreparedSourceCallLimits.MaximumReadBytes);
        Assert.Equal(64, PreparedSourceCallLimits.MaximumSearchMatches);
        Assert.Equal(1024 * 1024, PreparedSourceCallLimits.MaximumExaminedBytes);

        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        PreparedSourceIssueResult accepted = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                maximum));
        Assert.Equal(PreparedSourceIssueStatus.Issued, accepted.Status);
        Assert.NotNull(accepted.Context);
        await accepted.Context.DisposeAsync();

        PreparedSourceGrantLimits[] overMaximum =
        [
            maximum with { MaximumOperations = 101 },
            maximum with { MaximumMetadataEntries = 1_001 },
            maximum with { MaximumExaminedBytes = 8 * 1024 * 1024 + 1 },
            maximum with { MaximumReturnedBytes = 1024 * 1024 + 1 },
            maximum with { Deadline = TimeSpan.FromSeconds(30) + TimeSpan.FromTicks(1) },
        ];
        foreach (PreparedSourceGrantLimits limits in overMaximum)
        {
            PreparedSourceIssueResult issue = await issuer.IssueAsync(
                new PreparedSourceGrantRequest(
                    fixture.Approval,
                    fixture.PreparedRootPath,
                    limits));
            Assert.Equal(PreparedSourceIssueStatus.Denied, issue.Status);
            Assert.Null(issue.Context);
        }
    }

    private static SafeFileHandle OpenExclusiveDirectoryOrAssert(string path)
    {
        SafeFileHandle handle = CreateFileW(
            path,
            desiredAccess: 0x00000001,
            shareMode: 0,
            IntPtr.Zero,
            creationDisposition: 3,
            flagsAndAttributes: 0x02000000,
            IntPtr.Zero);
        Assert.False(handle.IsInvalid, $"Expected the disposed prepared-root handle to be released (Win32 error {Marshal.GetLastWin32Error()}).");
        return handle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    private static void RequireWindowsHost()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Windows handle-relative prepared-source access is not qualified on this platform.");
        }
    }

    private static async Task<IPreparedSourceReadContext> IssueContextAsync(
        PreparedSourceFixture fixture,
        IPreparedSourceCurrentAuthorityVerifier? authority = null,
        PreparedSourceGrantLimits? limits = null)
    {
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(authority ?? fixture.Authority);
        PreparedSourceIssueResult issue = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                limits ?? PreparedSourceGrantLimits.Default));
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.Context);
        return issue.Context;
    }

    private sealed class PausingPreparedSourceAuthority(
        PreparedSourceApproval approval,
        int pauseAtVerification) : IPreparedSourceCurrentAuthorityVerifier
    {
        private readonly TaskCompletionSource<bool> _paused =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _verificationCount;

        internal Task PausedVerificationEntered => _paused.Task;

        public async Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int verification = Interlocked.Increment(ref _verificationCount);
            if (verification == pauseAtVerification)
            {
                _paused.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return requestedApproval == approval
                ? new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current, approval)
                : new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
        }
    }

    private sealed class GatePreparedSourceAuthority(
        PreparedSourceApproval approval,
        int pauseAtVerification) : IPreparedSourceCurrentAuthorityVerifier
    {
        private readonly TaskCompletionSource<bool> _paused =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _verificationCount;

        internal Task PausedVerificationEntered => _paused.Task;

        internal void ReleasePausedVerification() => _release.TrySetResult(true);

        public async Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            CancellationToken cancellationToken = default)
        {
            int verification = Interlocked.Increment(ref _verificationCount);
            if (verification == pauseAtVerification)
            {
                _paused.TrySetResult(true);
                await _release.Task.WaitAsync(cancellationToken);
            }

            return requestedApproval == approval
                ? new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current, approval)
                : new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
        }
    }

    private sealed class RendezvousPreparedSourceAuthority(
        PreparedSourceApproval approval,
        int arrivals) : IPreparedSourceCurrentAuthorityVerifier
    {
        private readonly TaskCompletionSource<bool> _allArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _postIssueCallCount;
        private int _arrived;

        internal Task AllCallsArrived => _allArrived.Task;

        internal void ReleaseCalls() => _release.TrySetResult(true);

        public async Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            CancellationToken cancellationToken = default)
        {
            int call = Interlocked.Increment(ref _postIssueCallCount);
            if (call > 1 && call <= arrivals + 1)
            {
                if (Interlocked.Increment(ref _arrived) == arrivals)
                {
                    _allArrived.TrySetResult(true);
                }

                await _release.Task.WaitAsync(cancellationToken);
            }

            return requestedApproval == approval
                ? new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current, approval)
                : new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
        }
    }
}
