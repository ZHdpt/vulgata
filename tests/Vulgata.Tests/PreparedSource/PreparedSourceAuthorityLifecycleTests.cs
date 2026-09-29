using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedSourceAuthorityLifecycleTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("batch")]
    [InlineData("repository")]
    [InlineData("subject")]
    [InlineData("purpose")]
    [InlineData("approval")]
    [InlineData("root-binding")]
    [InlineData("operator-label")]
    public async Task ReadTextRangeAsync_ChangedApprovalTupleBeforeCall_IsDeniedWithoutPayload(string changedField)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        var authority = new TransitioningPreparedSourceAuthority(fixture.Approval);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        authority.SetCurrentApproval(ChangeField(fixture.Approval, changedField));

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", ByteOffset: 0, ByteCount: 5));

        Assert.Equal(PreparedSourceOutcome.Denied, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_VerifierUnavailableBeforeCall_ReturnsAuthorityUnavailable()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        var authority = new TransitioningPreparedSourceAuthority(fixture.Approval);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        authority.MakeUnavailable();

        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 100));

        Assert.Equal(PreparedSourceOutcome.AuthorityUnavailable, result.Outcome);
        Assert.Empty(result.Entries);
        Assert.Empty(result.Limitations);
        Assert.Null(result.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_AuthorityUnavailableBetweenEntries_DiscardsBufferedEntriesAndClosesContext()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        var authority = new TransitioningPreparedSourceAuthority(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(authority);
        PreparedSourceIssueResult issue = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedSourceGrantLimits.Default));
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.Context);
        await using IPreparedSourceReadContext context = issue.Context;

        // Verification 1 authorizes issuance; verification 2 starts ListAsync.
        // The next response must occur while a multi-entry result is being built,
        // before a later pre-release check can disclose any buffered entries.
        authority.SetStatusAtVerification(4, PreparedSourceAuthorityStatus.Unavailable);

        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 100));

        Assert.Equal(PreparedSourceOutcome.AuthorityUnavailable, result.Outcome);
        Assert.Empty(result.Entries);
        Assert.Empty(result.Limitations);
        Assert.Null(result.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchTextAsync_AuthorityUnavailableBetweenChunks_DiscardsBufferedMatches()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "chunk-set");
        Directory.CreateDirectory(searchRoot);
        byte[] content = Enumerable.Repeat((byte)'a', 8_192).ToArray();
        Array.Copy("match"u8.ToArray(), 0, content, 0, 5);
        await File.WriteAllBytesAsync(Path.Combine(searchRoot, "large.oddx"), content);
        var authority = new TransitioningPreparedSourceAuthority(fixture.Approval);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        // #1 issues the grant; #2 starts search; #3 authorizes the first chunk;
        // #4 occurs between the first and second bounded chunks.
        authority.SetStatusAtVerification(4, PreparedSourceAuthorityStatus.Unavailable);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("chunk-set", "match", PageSize: 64));

        Assert.Equal(PreparedSourceOutcome.AuthorityUnavailable, result.Outcome);
        Assert.Empty(result.Matches);
        Assert.Null(result.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchTextAsync_AuthorityUnavailableImmediatelyBeforeRelease_DiscardsMatchAndContinuation()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "release-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(Path.Combine(searchRoot, "matches.oddx"), "match match");
        var authority = new TransitioningPreparedSourceAuthority(fixture.Approval);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        // #1 issues the grant, #2 starts search, #3 authorizes its source chunk,
        // and #4 is the final authority check before returning buffered matches.
        authority.SetStatusAtVerification(4, PreparedSourceAuthorityStatus.Unavailable);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("release-set", "match", PageSize: 1));

        Assert.Equal(PreparedSourceOutcome.AuthorityUnavailable, result.Outcome);
        Assert.Empty(result.Matches);
        Assert.Null(result.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ConcurrentAuthorityLoss_ClosesBlockedSearchAndCurrentReadWithoutDisclosure()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "concurrent-set");
        Directory.CreateDirectory(searchRoot);
        byte[] content = Enumerable.Repeat((byte)'a', 8_192).ToArray();
        Array.Copy("match"u8.ToArray(), 0, content, 0, 5);
        await File.WriteAllBytesAsync(Path.Combine(searchRoot, "large.oddx"), content);
        var authority = new TransitioningPreparedSourceAuthority(fixture.Approval);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, authority);
        // Hold search between chunks, then make a concurrent read observe verifier
        // unavailability and close the shared grant before allowing search to resume.
        authority.PauseOnVerification(4);
        authority.SetStatusAtVerification(5, PreparedSourceAuthorityStatus.Unavailable);

        Task<PreparedSourceResult> searchTask = context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("concurrent-set", "match", PageSize: 64));
        Task<PreparedSourceResult>? readTask = null;
        PreparedSourceResult read;
        try
        {
            await authority.BlockedVerificationEntered.WaitAsync(TimeSpan.FromSeconds(5));
            readTask = context.ReadTextRangeAsync(
                new PreparedSourceReadTextRangeRequest("notes.oddlangx", ByteOffset: 0, ByteCount: 5));
            read = await readTask;
        }
        catch
        {
            authority.ReleaseBlockedVerification();
            await searchTask.WaitAsync(TimeSpan.FromSeconds(5));
            if (readTask is not null)
            {
                await readTask.WaitAsync(TimeSpan.FromSeconds(5));
            }

            throw;
        }

        authority.ReleaseBlockedVerification();
        PreparedSourceResult search = await searchTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(PreparedSourceOutcome.AuthorityUnavailable, read.Outcome);
        Assert.Null(read.Text);
        Assert.Null(read.ReadLocation);
        Assert.Equal(PreparedSourceOutcome.AuthorityUnavailable, search.Outcome);
        Assert.Empty(search.Matches);
        Assert.Null(search.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RevokedContinuation_AfterRestorationRequiresFreshGrant()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "continuation-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(Path.Combine(searchRoot, "items.oddx"), "x x x");
        var authority = new TransitioningPreparedSourceAuthority(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(authority);
        await using IPreparedSourceReadContext oldContext = await IssueContextAsync(fixture, authority);
        var request = new PreparedSourceSearchTextRequest("continuation-set", "x", PageSize: 1);
        PreparedSourceResult firstPage = await oldContext.SearchTextAsync(request);
        Assert.Equal(PreparedSourceOutcome.Limited, firstPage.Outcome);
        Assert.NotNull(firstPage.Continuation);

        authority.Revoke();
        PreparedSourceResult revokedPage = await oldContext.SearchTextAsync(request with
        {
            Continuation = firstPage.Continuation,
        });

        Assert.Equal(PreparedSourceOutcome.Denied, revokedPage.Outcome);
        Assert.Empty(revokedPage.Matches);
        Assert.Null(revokedPage.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, oldContext.Lifetime);

        authority.SetCurrentApproval(fixture.Approval);
        PreparedSourceResult oldContextAfterRestore = await oldContext.SearchTextAsync(request);
        Assert.Equal(PreparedSourceOutcome.Denied, oldContextAfterRestore.Outcome);
        Assert.Empty(oldContextAfterRestore.Matches);

        await using IPreparedSourceReadContext freshContext = await IssueContextAsync(fixture, authority);
        PreparedSourceResult freshGrantResult = await freshContext.SearchTextAsync(request with { PageSize = 64 });
        Assert.Equal(PreparedSourceOutcome.Complete, freshGrantResult.Outcome);
        Assert.Equal(3, freshGrantResult.Matches.Count);
        Assert.Equal(PreparedSourceOutcome.Denied,
            (await freshContext.SearchTextAsync(request with { Continuation = firstPage.Continuation })).Outcome);
    }

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
        TransitioningPreparedSourceAuthority authority)
    {
        var issuer = new PreparedSourceContextIssuer(authority);
        PreparedSourceIssueResult issue = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedSourceGrantLimits.Default));
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.Context);
        return issue.Context;
    }

    private static PreparedSourceApproval ChangeField(PreparedSourceApproval approval, string field) =>
        field switch
        {
            "batch" => approval with { BatchId = Guid.NewGuid() },
            "repository" => approval with { RepositoryId = Guid.NewGuid() },
            "subject" => approval with { SubjectId = "different-subject" },
            "purpose" => approval with { Purpose = (PreparedSourcePurpose)999 },
            "approval" => approval with { ApprovalIdentity = "different-approval" },
            "root-binding" => approval with { RootBindingIdentity = "different-root" },
            "operator-label" => approval with { OperatorLabel = "different descriptive label" },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown approval field."),
        };

    private sealed class TransitioningPreparedSourceAuthority : IPreparedSourceCurrentAuthorityVerifier
    {
        private readonly object _sync = new();
        private readonly Dictionary<int, PreparedSourceAuthorityStatus> _scheduledStatuses = [];
        private readonly TaskCompletionSource<bool> _blockedVerificationEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource<bool>? _releaseBlockedVerification;
        private PreparedSourceApproval _currentApproval;
        private int? _pauseVerificationNumber;
        private int _verificationCount;
        private PreparedSourceAuthorityStatus _currentStatus = PreparedSourceAuthorityStatus.Current;

        internal TransitioningPreparedSourceAuthority(PreparedSourceApproval approval)
        {
            _currentApproval = approval;
        }

        internal Task BlockedVerificationEntered => _blockedVerificationEntered.Task;

        internal void SetCurrentApproval(PreparedSourceApproval approval)
        {
            lock (_sync)
            {
                _currentApproval = approval;
                _currentStatus = PreparedSourceAuthorityStatus.Current;
            }
        }

        internal void Revoke()
        {
            lock (_sync)
            {
                _currentStatus = PreparedSourceAuthorityStatus.Revoked;
            }
        }

        internal void MakeUnavailable()
        {
            lock (_sync)
            {
                _currentStatus = PreparedSourceAuthorityStatus.Unavailable;
            }
        }

        internal void SetStatusAtVerification(int verificationNumber, PreparedSourceAuthorityStatus status)
        {
            lock (_sync)
            {
                _scheduledStatuses[verificationNumber] = status;
            }
        }

        internal void PauseOnVerification(int verificationNumber)
        {
            lock (_sync)
            {
                _pauseVerificationNumber = verificationNumber;
                _releaseBlockedVerification = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        internal void ReleaseBlockedVerification()
        {
            TaskCompletionSource<bool>? release;
            lock (_sync)
            {
                release = _releaseBlockedVerification;
            }

            release?.TrySetResult(true);
        }

        public async Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task? releaseTask = null;
            lock (_sync)
            {
                _verificationCount++;
                if (_scheduledStatuses.TryGetValue(_verificationCount, out PreparedSourceAuthorityStatus scheduled))
                {
                    _currentStatus = scheduled;
                }

                if (_pauseVerificationNumber == _verificationCount)
                {
                    _pauseVerificationNumber = null;
                    _blockedVerificationEntered.TrySetResult(true);
                    releaseTask = _releaseBlockedVerification!.Task;
                }
            }

            if (releaseTask is not null)
            {
                await releaseTask.WaitAsync(cancellationToken);
            }

            lock (_sync)
            {
                if (_currentStatus != PreparedSourceAuthorityStatus.Current)
                {
                    return new PreparedSourceAuthorityResult(_currentStatus, null);
                }

                if (requestedApproval != _currentApproval)
                {
                    return new PreparedSourceAuthorityResult(
                        PreparedSourceAuthorityStatus.Revoked,
                        null);
                }

                return new PreparedSourceAuthorityResult(
                    PreparedSourceAuthorityStatus.Current,
                    _currentApproval);
            }
        }
    }
}
