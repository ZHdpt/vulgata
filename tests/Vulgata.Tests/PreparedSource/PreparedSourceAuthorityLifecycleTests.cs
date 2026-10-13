using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.OctoberGenerator;
using System.Text;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedSourceAuthorityLifecycleTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("business")]
    [InlineData("worker")]
    [InlineData("operations")]
    [InlineData("metadata")]
    [InlineData("examined")]
    [InlineData("returned")]
    [InlineData("deadline")]
    public async Task BusinessSourcePurpose_TwoPassGrantLimitsArePurposeSpecificAndCannotBeEnlargedAsync(string boundary)
    {
        RequireWindowsHost();
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var approval = fixture.Approval with { Purpose = boundary == "worker"
            ? PreparedSourcePurpose.WorkerSourceRead : PreparedSourcePurpose.BusinessSynthesisRead };
        var limits = BusinessReadLimits();
        limits = boundary switch
        {
            "operations" => limits with { MaximumOperations = 513 },
            "metadata" => limits with { MaximumMetadataEntries = 2 },
            "examined" => limits with { MaximumExaminedBytes = 16L * 1024 * 1024 + 1 },
            "returned" => limits with { MaximumReturnedBytes = 8L * 1024 * 1024 + 1 },
            "deadline" => limits with { Deadline = TimeSpan.FromSeconds(30) + TimeSpan.FromTicks(1) },
            _ => limits
        };
        var issuer = new PreparedSourceContextIssuer(new TransitioningPreparedSourceAuthority(approval));
        var result = await issuer.IssueAsync(new(approval, fixture.PreparedRootPath, limits));
        try
        {
            Assert.Equal(boundary == "business" ? PreparedSourceIssueStatus.Issued : PreparedSourceIssueStatus.Denied, result.Status);
            if (boundary == "business") Assert.Equal(PreparedSourceContextLifetime.Open, Assert.IsAssignableFrom<IPreparedSourceReadContext>(result.Context).Lifetime);
            else Assert.Null(result.Context);
        }
        finally { if (result.Context is not null) await result.Context.DisposeAsync(); }
    }

    private static PreparedSourceGrantLimits BusinessReadLimits() => new(512, 1, 16L * 1024 * 1024,
        8L * 1024 * 1024, TimeSpan.FromSeconds(30));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BusinessReferencedReader_Utf8PagesReconstructOnlyExactCitedCrlfSpanAsync()
    {
        RequireWindowsHost();
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        const string content = "\uFEFFheader\r\n你🙂é尾\r\nprivate-neighbor\n";
        await File.WriteAllTextAsync(fixture.UnfamiliarTextPath, content, new UTF8Encoding(false, true));
        var anchor = BusinessAnchor(fixture, content, 2, 2);
        int closes = 0;
        var contexts = new List<IPreparedSourceReadContext>();
        var reader = new OctoberBusinessSourceReader(async (requested, token) =>
        {
            Assert.Equal(anchor, requested);
            var context = await IssueContextAsync(fixture, new TransitioningPreparedSourceAuthority(fixture.Approval));
            contexts.Add(context);
            return new(context, _ => Task.FromResult(DisclosureStatus.Allowed), _ => Task.FromResult<string?>("same-contained-file"),
                () => { closes++; return ValueTask.CompletedTask; });
        });
        var text = new StringBuilder(); int offset = 0; int pages = 0;
        while (true)
        {
            var page = await reader.ReadAsync(anchor, offset, 4, 16 * 1024 * 1024, default);
            Assert.Contains(page.Status, new[] { BusinessSourceStatus.Complete, BusinessSourceStatus.Limited });
            Assert.Equal(anchor, page.Source); Assert.Equal(offset, page.Offset);
            Assert.NotNull(page.Text); Assert.DoesNotContain("header", page.Text); Assert.DoesNotContain("private-neighbor", page.Text);
            int bytes = new UTF8Encoding(false, true).GetByteCount(page.Text!);
            Assert.InRange(bytes, 1, 4); text.Append(page.Text); pages++;
            Assert.InRange(pages, 1, 10);
            if (page.Status == BusinessSourceStatus.Complete) { Assert.Null(page.NextOffset); break; }
            Assert.Equal(offset + bytes, page.NextOffset); offset = page.NextOffset!.Value;
        }
        Assert.Equal("你🙂é尾\r\n", text.ToString());
        Assert.True(pages > 1); Assert.Equal(pages, closes);
        Assert.All(contexts, context => Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("traversal")]
    [InlineData("absolute")]
    [InlineData("negative-offset")]
    [InlineData("reversed-lines")]
    [InlineData("unaffordable-probes")]
    public async Task BusinessReferencedReader_InvalidRequestNeverOpensAGrantAsync(string boundary)
    {
        var anchor = new SourceAnchor(new(Guid.NewGuid(), "controlled-root", "src/retained.cs", new string('a', 64), "original-version"), 1, 2);
        if (boundary == "traversal") anchor = anchor with { Origin = anchor.Origin with { RelativePath = "../outside.cs" } };
        if (boundary == "absolute") anchor = anchor with { Origin = anchor.Origin with { RelativePath = "C:/outside.cs" } };
        if (boundary == "reversed-lines") anchor = anchor with { StartLine = 3 };
        int opens = 0;
        var reader = new OctoberBusinessSourceReader((_, _) => { opens++; throw new InvalidOperationException("A structurally invalid request must not open a grant."); });
        var page = await reader.ReadAsync(anchor, boundary == "negative-offset" ? -1 : 0, 32 * 1024,
            boundary == "unaffordable-probes" ? 11 : 16 * 1024 * 1024, default);
        Assert.Equal(boundary == "unaffordable-probes" ? BusinessSourceStatus.BudgetExceeded : BusinessSourceStatus.Denied, page.Status);
        Assert.Null(page.Text); Assert.Equal(0, page.ExaminedBytes); Assert.Equal(0, opens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusinessReferencedReader_ChargesFailedRangesAndNeverDispatchesAnUnaffordableSecondPassAsync(bool failFirstRange)
    {
        RequireWindowsHost();
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var inner = await IssueContextAsync(fixture, new TransitioningPreparedSourceAuthority(fixture.Approval));
        var observed = new BusinessObservedReadContext(inner) { FailFirstRange = failFirstRange };
        var anchor = BusinessAnchor(fixture, "café\nsource fixture\n", 2, 2);
        var reader = new OctoberBusinessSourceReader((_, _) => Task.FromResult<OctoberBusinessSourceGrant?>(new(observed,
            _ => Task.FromResult(DisclosureStatus.Allowed), _ => Task.FromResult<string?>("same-file"))));
        var page = await reader.ReadAsync(anchor, 0, 32 * 1024, failFirstRange ? 16 * 1024 * 1024 : 100, default);
        Assert.Equal(observed.Requests.Sum(request => (long)request.ByteCount + 11), page.ExaminedBytes);
        if (failFirstRange)
        {
            Assert.Equal(BusinessSourceStatus.Complete, page.Status); Assert.Equal("source fixture\n", page.Text);
            Assert.True(observed.Requests.Count >= 3);
            Assert.True(page.ExaminedBytes > observed.Requests.Skip(1).Sum(request => (long)request.ByteCount + 11));
        }
        else
        {
            Assert.Equal(BusinessSourceStatus.BudgetExceeded, page.Status); Assert.Null(page.Text);
            Assert.Single(observed.Requests); Assert.Equal(100, page.ExaminedBytes);
        }
        Assert.Equal(PreparedSourceContextLifetime.Disposed, inner.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusinessReferencedReader_AuthorityOrIdentityChangesAfterFirstPass_DiscardsBufferedTextAsync(bool identityChanges)
    {
        RequireWindowsHost();
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var inner = await IssueContextAsync(fixture, new TransitioningPreparedSourceAuthority(fixture.Approval));
        var observed = new BusinessObservedReadContext(inner);
        var anchor = BusinessAnchor(fixture, "café\nsource fixture\n", 2, 2);
        var reader = new OctoberBusinessSourceReader((_, _) => Task.FromResult<OctoberBusinessSourceGrant?>(new(observed,
            _ => Task.FromResult(!identityChanges && observed.Requests.Count > 0 ? DisclosureStatus.Denied : DisclosureStatus.Allowed),
            _ => Task.FromResult<string?>(identityChanges && observed.Requests.Count > 0 ? "replaced-file" : "original-file"))));
        var page = await reader.ReadAsync(anchor, 0, 32 * 1024, 16 * 1024 * 1024, default);
        Assert.Equal(identityChanges ? BusinessSourceStatus.Changed : BusinessSourceStatus.Denied, page.Status);
        Assert.Null(page.Text); Assert.Null(page.NextOffset); Assert.Single(observed.Requests);
        Assert.True(page.ExaminedBytes > 0); Assert.Equal(PreparedSourceContextLifetime.Disposed, inner.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BusinessReferencedReader_UnconfirmedApprovalCleanupPropagatesInsteadOfReturningRetryablePageAsync()
    {
        RequireWindowsHost();
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var context = await IssueContextAsync(fixture, new TransitioningPreparedSourceAuthority(fixture.Approval));
        var anchor = BusinessAnchor(fixture, "café\nsource fixture\n", 2, 2);
        int closes = 0;
        var reader = new OctoberBusinessSourceReader((_, _) => Task.FromResult<OctoberBusinessSourceGrant?>(new(context,
            _ => Task.FromResult(DisclosureStatus.Allowed), _ => Task.FromResult<string?>("same-file"),
            () => { closes++; throw new IOException("Controlled approval cleanup could not be confirmed."); })));
        await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync(anchor, 0, 32 * 1024, 16 * 1024 * 1024, default));
        Assert.Equal(1, closes); Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
    }

    private static SourceAnchor BusinessAnchor(PreparedSourceFixture fixture, string content, int start, int end) => new(
        new(fixture.Approval.RepositoryId, fixture.Approval.RootBindingIdentity, "notes.oddlangx", OctoberKnowledgePolicy.Hash(content), "original-cl-version"), start, end);

    private sealed class BusinessObservedReadContext(IPreparedSourceReadContext inner) : IPreparedSourceReadContext
    {
        public bool FailFirstRange { get; init; }
        public List<PreparedSourceReadTextRangeRequest> Requests { get; } = [];
        public Guid RepositoryId => inner.RepositoryId;
        public PreparedSourceContextLifetime Lifetime => inner.Lifetime;
        public Task<PreparedSourceResult> ListAsync(PreparedSourceListRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PreparedSourceResult> SearchTextAsync(PreparedSourceSearchTextRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PreparedSourceResult> ReadTextRangeAsync(PreparedSourceReadTextRangeRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (FailFirstRange && Requests.Count == 1) return Task.FromResult(new PreparedSourceResult
                { RepositoryId = RepositoryId, Operation = PreparedSourceOperation.ReadTextRange, Outcome = PreparedSourceOutcome.InvalidRange });
            return inner.ReadTextRangeAsync(request, cancellationToken);
        }
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(PreparedToolPurpose.JdtNavigation)]
    [InlineData(PreparedToolPurpose.CodeGraphPreparation)]
    public async Task BusinessSourcePurpose_CannotIssueWorkerToolWorkspaceEvenWithCurrentSourceGrantAsync(PreparedToolPurpose toolPurpose)
    {
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var approval = fixture.Approval with { Purpose = PreparedSourcePurpose.BusinessSynthesisRead };
        var toolAuthority = new BusinessPurposeToolAuthority();
        var issuer = new PreparedToolWorkspaceIssuer(new TransitioningPreparedSourceAuthority(approval), toolAuthority);
        var result = await issuer.IssueAsync(new(approval, fixture.PreparedRootPath, toolPurpose, TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Denied, result.Status); Assert.Null(result.Lease);
        Assert.Equal(0, toolAuthority.Calls);
    }

    private sealed class BusinessPurposeToolAuthority : IPreparedToolUseAuthorizer
    {
        public int Calls { get; private set; }
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(PreparedSourceApproval approval, PreparedToolPurpose toolPurpose, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new PreparedToolUseAuthorityResult(PreparedToolUseAuthorityStatus.Current));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusinessReferencedReader_VerifiesOriginalBytesAndReturnsOnlyTheCitedLineAsync(bool changedDigest)
    {
        RequireWindowsHost();
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var authority = new TransitioningPreparedSourceAuthority(fixture.Approval);
        // Existing contained-context fixture isolates the reader algorithm. Separate Business-purpose
        // tests qualify issuance; this controlled grant does not claim real Business authentication.
        var context = await IssueContextAsync(fixture, authority);
        var origin = new PreparedOrigin(fixture.Approval.RepositoryId, fixture.Approval.RootBindingIdentity,
            "notes.oddlangx", changedDigest ? new string('0', 64) : OctoberKnowledgePolicy.Hash("café\nsource fixture\n"), "original-cl-prepared-version");
        var anchor = new SourceAnchor(origin, 2, 2);
        int opens = 0, closes = 0;
        var reader = new OctoberBusinessSourceReader((requested, token) =>
        {
            token.ThrowIfCancellationRequested(); Assert.Equal(anchor, requested); opens++;
            return Task.FromResult<OctoberBusinessSourceGrant?>(new(context,
                _ => Task.FromResult(DisclosureStatus.Allowed), _ => Task.FromResult<string?>("controlled-stable-file-identity"),
                () => { closes++; return ValueTask.CompletedTask; }));
        });
        var page = await reader.ReadAsync(anchor, 0, 32 * 1024, 16 * 1024 * 1024, default);
        Assert.Equal(changedDigest ? BusinessSourceStatus.Changed : BusinessSourceStatus.Complete, page.Status);
        Assert.Equal(anchor, page.Source); Assert.Equal(0, page.Offset); Assert.Null(page.NextOffset);
        if (changedDigest) Assert.Null(page.Text);
        else { Assert.Equal("source fixture\n", page.Text); Assert.DoesNotContain("café", page.Text, StringComparison.Ordinal); }
        Assert.True(page.ExaminedBytes > 0); // A conservative charged bound, not an exact I/O measurement.
        Assert.Equal(1, opens); Assert.Equal(1, closes);
        Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BusinessSourcePurpose_ExactGrantCanReadAndLiveRevocationClosesDisclosureAsync()
    {
        RequireWindowsHost();
        await using var fixture = await PreparedSourceFixture.CreateAsync();
        var approval = fixture.Approval with { Purpose = PreparedSourcePurpose.BusinessSynthesisRead };
        var authority = new TransitioningPreparedSourceAuthority(approval);
        var issuer = new PreparedSourceContextIssuer(authority);
        var issued = await issuer.IssueAsync(new(approval, fixture.PreparedRootPath, BusinessReadLimits()));
        Assert.Equal(PreparedSourceIssueStatus.Issued, issued.Status);
        await using var context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(issued.Context);
        var read = await context.ReadTextRangeAsync(new("notes.oddlangx", ByteOffset: 0, ByteCount: 5));
        Assert.Equal(PreparedSourceOutcome.Complete, read.Outcome);
        Assert.Equal("café", read.Text);
        authority.Revoke();
        var revoked = await context.ReadTextRangeAsync(new("notes.oddlangx", ByteOffset: 0, ByteCount: 5));
        Assert.Equal(PreparedSourceOutcome.Denied, revoked.Outcome);
        Assert.Null(revoked.Text); Assert.Null(revoked.ReadLocation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("current")]
    [InlineData("worker-substitution")]
    [InlineData("undefined-purpose")]
    [InlineData("root-revoked")]
    public async Task BusinessSourcePurpose_VerifierRequiresExactPurposeAndCurrentRootAsync(string boundary)
    {
        var requested = new PreparedSourceApproval(Guid.NewGuid(), Guid.NewGuid(), "controlled-operator",
            boundary == "undefined-purpose" ? (PreparedSourcePurpose)999 : PreparedSourcePurpose.BusinessSynthesisRead,
            "controlled-business-approval", "controlled-root", "Controlled source");
        int checks = 0;
        var verifier = new OctoberPreparedSourceAuthorityVerifier("controlled-target", (target, approval, token) =>
        {
            checks++; Assert.Equal("controlled-target", target); Assert.Equal(requested, approval);
            return Task.FromResult(new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current,
                boundary == "worker-substitution" ? approval with { Purpose = PreparedSourcePurpose.WorkerSourceRead } : approval));
        }, _ => boundary != "root-revoked");
        var result = await verifier.VerifyCurrentAsync(requested);
        Assert.Equal(boundary == "current" ? PreparedSourceAuthorityStatus.Current : PreparedSourceAuthorityStatus.Revoked, result.Status);
        Assert.Equal(boundary == "current" ? requested : null, result.CurrentApproval);
        Assert.Equal(boundary == "undefined-purpose" ? 0 : 1, checks);
    }

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
