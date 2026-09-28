using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Tests.PreparedSource;

public sealed class PreparedSourceRangeReadTests
{
    [Fact]
    public async Task ReadTextRangeAsync_NonzeroOffset_UsesZeroBasedUtf8ByteLocations()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", ByteOffset: 6, ByteCount: 6));

        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal("source", result.Text);
        Assert.Equal(
            new PreparedSourceReadLocation(
                fixture.Approval.RepositoryId,
                "notes.oddlangx",
                ByteStart: 6,
                ByteEndExclusive: 12),
            result.ReadLocation);
        Assert.Equal(21L, result.ObservedFileByteLength);
    }

    [Fact]
    public async Task ReadTextRangeAsync_OffsetStartsInsideMultibyteScalar_ReturnsInvalidRange()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest(
                RelativePath: "notes.oddlangx",
                ByteOffset: 4,
                ByteCount: 1));

        Assert.Equal(PreparedSourceOutcome.InvalidRange, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
    }

    [Fact]
    public async Task ReadTextRangeAsync_SliceEndsInsideMultibyteScalar_ReturnsInvalidRange()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", ByteOffset: 0, ByteCount: 4));

        Assert.Equal(PreparedSourceOutcome.InvalidRange, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65_536)]
    public async Task ReadTextRangeAsync_ByteCountAtSupportedBounds_ReturnsRequestedBytes(int byteCount)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("long.payloadx", ByteOffset: 0, byteCount));

        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal(new string('L', byteCount), result.Text);
        Assert.Equal(0, result.ReadLocation?.ByteStart);
        Assert.Equal(byteCount, result.ReadLocation?.ByteEndExclusive);
        Assert.Equal(70_000L, result.ObservedFileByteLength);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 65_537)]
    public async Task ReadTextRangeAsync_InvalidOffsetOrByteCount_ReturnsInvalidRange(long byteOffset, int byteCount)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", byteOffset, byteCount));

        Assert.Equal(PreparedSourceOutcome.InvalidRange, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
    }

    [Fact]
    public async Task ReadTextRangeAsync_InvalidUtf8Source_ReturnsUnsupportedTextWithoutPayload()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("invalid.bytesx", ByteOffset: 0, ByteCount: 3));

        Assert.Equal(PreparedSourceOutcome.UnsupportedText, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
    }

    [Fact]
    public async Task ReadTextRangeAsync_InitialUtf8Bom_IsAccepted()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string relativePath = "bom.contentx";
        await File.WriteAllBytesAsync(
            Path.Combine(fixture.PreparedRootPath, relativePath),
            [0xEF, 0xBB, 0xBF, 0x68, 0x65, 0x6C, 0x6C, 0x6F]);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest(relativePath, ByteOffset: 0, ByteCount: 8));

        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.NotNull(result.Text);
        Assert.Equal("hello", result.Text.TrimStart('\uFEFF'));
        Assert.Equal(0, result.ReadLocation?.ByteStart);
        Assert.Equal(8, result.ReadLocation?.ByteEndExclusive);
    }

    [Fact]
    public async Task ReadTextRangeAsync_EmptyFileAndExactEof_ReturnCompleteEmpty()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult empty = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("empty.datax", ByteOffset: 0, ByteCount: 1));
        PreparedSourceResult eof = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", ByteOffset: 21, ByteCount: 1));

        Assert.Equal(PreparedSourceOutcome.CompleteEmpty, empty.Outcome);
        Assert.Equal(string.Empty, empty.Text);
        Assert.Equal(0, empty.ReadLocation?.ByteStart);
        Assert.Equal(0, empty.ReadLocation?.ByteEndExclusive);
        Assert.Equal(PreparedSourceOutcome.CompleteEmpty, eof.Outcome);
        Assert.Equal(string.Empty, eof.Text);
        Assert.Equal(21, eof.ReadLocation?.ByteStart);
        Assert.Equal(21, eof.ReadLocation?.ByteEndExclusive);
    }

    [Fact]
    public async Task ReadTextRangeAsync_OffsetBeyondEof_ReturnsInvalidRange()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", ByteOffset: 22, ByteCount: 1));

        Assert.Equal(PreparedSourceOutcome.InvalidRange, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
    }

    [Fact]
    public async Task ReadTextRangeAsync_GrantReturnedByteCap_ReportsExhaustionWithoutNextOffset()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantLimits limits = PreparedSourceGrantLimits.Default with
        {
            MaximumReturnedBytes = 3,
        };
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, limits);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("long.payloadx", ByteOffset: 0, ByteCount: 10));

        Assert.Equal(PreparedSourceOutcome.Limited, result.Outcome);
        Assert.Equal("LLL", result.Text);
        Assert.Equal(PreparedSourceStoppingLimit.GrantReturnedBytes, result.StoppingLimit);
        Assert.Equal(0, result.ReadLocation?.ByteStart);
        Assert.Equal(3, result.ReadLocation?.ByteEndExclusive);
        Assert.Null(result.NextByteOffset);

        PreparedSourceResult exhausted = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("long.payloadx", ByteOffset: 3, ByteCount: 3));

        Assert.Equal(PreparedSourceOutcome.Limited, exhausted.Outcome);
        Assert.Equal(PreparedSourceStoppingLimit.GrantReturnedBytes, exhausted.StoppingLimit);
        Assert.Null(exhausted.Text);
        Assert.Null(exhausted.ReadLocation);
        Assert.Null(exhausted.NextByteOffset);
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
