using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Tests.PreparedSource;

public sealed class PreparedSourceSearchTests
{
    [Fact]
    public async Task SearchTextAsync_UnfamiliarExtension_ReturnsExactUtf8MatchLocation()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "extension-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(
            Path.Combine(searchRoot, "notes.oddlangx"),
            "café\nsource fixture\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest(
                RelativeDirectory: "extension-set",
                Literal: "source",
                PageSize: 64));

        Assert.Equal(PreparedSourceOperation.SearchText, result.Operation);
        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
        PreparedSourceMatch match = Assert.Single(result.Matches);
        Assert.Equal(
            new PreparedSourceMatchLocation(
                fixture.Approval.RepositoryId,
                "extension-set/notes.oddlangx",
                ByteStart: 6,
                ByteLength: 6,
                Line: 2,
                UnicodeScalarColumn: 1),
            match.Location);
        Assert.Null(result.Continuation);
    }

    [Fact]
    public async Task SearchTextAsync_MultipleFiles_OrdersByPathThenByteAndCountsUnicodeScalarsAcrossCrlf()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "search-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(
            Path.Combine(searchRoot, "a.oddx"),
            "α target target\r\nz target",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await File.WriteAllTextAsync(
            Path.Combine(searchRoot, "z.unknownx"),
            "target",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("search-set", "target", PageSize: 64));

        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Collection(
            result.Matches,
            match => Assert.Equal(
                new PreparedSourceMatchLocation(fixture.Approval.RepositoryId, "search-set/a.oddx", 3, 6, 1, 3),
                match.Location),
            match => Assert.Equal(
                new PreparedSourceMatchLocation(fixture.Approval.RepositoryId, "search-set/a.oddx", 10, 6, 1, 10),
                match.Location),
            match => Assert.Equal(
                new PreparedSourceMatchLocation(fixture.Approval.RepositoryId, "search-set/a.oddx", 20, 6, 2, 3),
                match.Location),
            match => Assert.Equal(
                new PreparedSourceMatchLocation(fixture.Approval.RepositoryId, "search-set/z.unknownx", 0, 6, 1, 1),
                match.Location));
        Assert.Null(result.Continuation);
    }

    [Fact]
    public async Task SearchTextAsync_CaseSensitiveLiteralWith256Utf8Bytes_IsAcceptedAndCanBeCompleteEmpty()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "query-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(Path.Combine(searchRoot, "lower.oddx"), "source");
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult caseSensitive = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("query-set", "Source", PageSize: 1));
        PreparedSourceResult maximumLiteral = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("query-set", new string('é', 128), PageSize: 1));

        Assert.Equal(PreparedSourceOutcome.CompleteEmpty, caseSensitive.Outcome);
        Assert.Empty(caseSensitive.Matches);
        Assert.Equal(PreparedSourceOutcome.CompleteEmpty, maximumLiteral.Outcome);
        Assert.Empty(maximumLiteral.Matches);
    }

    [Theory]
    [InlineData("")]
    [InlineData("two\nlines")]
    [InlineData("two\r\nlines")]
    public async Task SearchTextAsync_EmptyOrMultilineLiteral_IsDeniedWithoutMatches(string literal)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("", literal, PageSize: 1));

        Assert.Equal(PreparedSourceOutcome.Denied, result.Outcome);
        Assert.Empty(result.Matches);
        Assert.Null(result.Continuation);
    }

    [Fact]
    public async Task SearchTextAsync_LiteralOver256Utf8Bytes_IsDenied()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("", new string('é', 129), PageSize: 1));

        Assert.Equal(PreparedSourceOutcome.Denied, result.Outcome);
        Assert.Empty(result.Matches);
        Assert.Null(result.Continuation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public async Task SearchTextAsync_PageSizeOutsideOneThrough64_IsDenied(int pageSize)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("", "source", pageSize));

        Assert.Equal(PreparedSourceOutcome.Denied, result.Outcome);
        Assert.Empty(result.Matches);
        Assert.Null(result.Continuation);
    }

    [Fact]
    public async Task SearchTextAsync_PageSizeOne_ContinuesOnceAndRejectsReplay()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "page-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(Path.Combine(searchRoot, "items.oddx"), "x x x");
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult first = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("page-set", "x", PageSize: 1));
        PreparedSourceResult second = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("page-set", "x", PageSize: 1, first.Continuation));
        PreparedSourceResult replay = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("page-set", "x", PageSize: 1, first.Continuation));

        Assert.Equal(PreparedSourceOutcome.Limited, first.Outcome);
        Assert.Equal(new PreparedSourceMatchLocation(fixture.Approval.RepositoryId, "page-set/items.oddx", 0, 1, 1, 1),
            Assert.Single(first.Matches).Location);
        Assert.NotNull(first.Continuation);
        Assert.Equal(PreparedSourceOutcome.Limited, second.Outcome);
        Assert.Equal(new PreparedSourceMatchLocation(fixture.Approval.RepositoryId, "page-set/items.oddx", 2, 1, 1, 3),
            Assert.Single(second.Matches).Location);
        Assert.NotNull(second.Continuation);
        Assert.Equal(PreparedSourceOutcome.Denied, replay.Outcome);
        Assert.Empty(replay.Matches);
        Assert.Null(replay.Continuation);
    }

    [Fact]
    public async Task SearchTextAsync_ContinuationBoundToLiteral_IsDeniedForChangedQuery()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "scope-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(Path.Combine(searchRoot, "items.oddx"), "x x");
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        PreparedSourceResult first = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("scope-set", "x", PageSize: 1));
        Assert.Equal(PreparedSourceOutcome.Limited, first.Outcome);
        Assert.NotNull(first.Continuation);

        PreparedSourceResult changedQuery = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("scope-set", "y", PageSize: 1, first.Continuation));

        Assert.Equal(PreparedSourceOutcome.Denied, changedQuery.Outcome);
        Assert.Empty(changedQuery.Matches);
        Assert.Null(changedQuery.Continuation);
    }

    [Fact]
    public async Task SearchTextAsync_ExaminedByteBudgetExhaustion_IsExplicitForNoMatch()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "limit-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(Path.Combine(searchRoot, "content.oddx"), "abcdef");
        PreparedSourceGrantLimits limits = PreparedSourceGrantLimits.Default with
        {
            MaximumExaminedBytes = 1,
        };
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, limits);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("limit-set", "z", PageSize: 64));

        Assert.Equal(PreparedSourceOutcome.Limited, result.Outcome);
        Assert.Equal(PreparedSourceStoppingLimit.GrantExaminedBytes, result.StoppingLimit);
        Assert.Empty(result.Matches);
        Assert.Null(result.Continuation);
    }

    [Fact]
    public async Task SearchTextAsync_PerCallExaminedByteLimit_ResumesWithRemainingGrantCapacity()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        const int perCallExaminedByteLimit = 1_048_576;
        const int bytesAfterFirstCallLimit = 4_096;
        const string literal = "needle";
        int matchOffset = perCallExaminedByteLimit + bytesAfterFirstCallLimit;
        byte[] content = new byte[matchOffset + literal.Length];
        Array.Fill(content, (byte)'a', 0, matchOffset);
        Array.Copy(Encoding.ASCII.GetBytes(literal), 0, content, matchOffset, literal.Length);

        string searchRoot = Path.Combine(fixture.PreparedRootPath, "call-budget-set");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllBytesAsync(Path.Combine(searchRoot, "large.oddx"), content);
        PreparedSourceGrantLimits limits = PreparedSourceGrantLimits.Default with
        {
            MaximumExaminedBytes = 2 * 1024 * 1024,
        };
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, limits);
        var request = new PreparedSourceSearchTextRequest("call-budget-set", literal, PageSize: 64);

        PreparedSourceResult first = await context.SearchTextAsync(request);

        Assert.Equal(PreparedSourceOutcome.Limited, first.Outcome);
        Assert.Equal(PreparedSourceStoppingLimit.CallExaminedBytes, first.StoppingLimit);
        Assert.Empty(first.Matches);
        Assert.NotNull(first.Continuation);

        PreparedSourceResult resumed = await context.SearchTextAsync(request with
        {
            Continuation = first.Continuation,
        });

        Assert.Equal(PreparedSourceOutcome.Complete, resumed.Outcome);
        Assert.Null(resumed.Continuation);
        Assert.Equal(
            new PreparedSourceMatchLocation(
                fixture.Approval.RepositoryId,
                "call-budget-set/large.oddx",
                matchOffset,
                literal.Length,
                Line: 1,
                UnicodeScalarColumn: matchOffset + 1),
            Assert.Single(resumed.Matches).Location);

        PreparedSourceResult replay = await context.SearchTextAsync(request with
        {
            Continuation = first.Continuation,
        });

        Assert.Equal(PreparedSourceOutcome.Denied, replay.Outcome);
        Assert.Empty(replay.Matches);
        Assert.Null(replay.Continuation);
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
        PreparedSourceGrantLimits? limits = null)
    {
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        PreparedSourceIssueResult issue = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                limits ?? PreparedSourceGrantLimits.Default));

        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.Context);
        return issue.Context;
    }
}
