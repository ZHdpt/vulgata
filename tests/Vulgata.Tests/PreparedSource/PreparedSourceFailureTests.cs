using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedSource;

public sealed class PreparedSourceFailureTests
{
    [Fact]
    public async Task ReadTextRangeAsync_MissingFile_ReturnsMissingWithoutSensitiveDetails()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("not-present.privatex", 0, 16));

        Assert.Equal(PreparedSourceOutcome.Missing, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.ObservedFileByteLength);
        AssertSanitized(result, fixture, "not-present.privatex");
    }

    [Fact]
    public async Task ReadTextRangeAsync_FileUnavailableForIo_ReturnsUnreadableWithoutPayload()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string relativePath = "read-locked.privatex";
        string sentinelSource = "secret source bytes that must not escape";
        string physicalPath = Path.Combine(fixture.PreparedRootPath, relativePath);
        await File.WriteAllTextAsync(physicalPath, sentinelSource, new UTF8Encoding(false));
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        await using var exclusive = new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest(relativePath, 0, 32));

        Assert.Equal(PreparedSourceOutcome.Unreadable, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.ObservedFileByteLength);
        AssertSanitized(result, fixture, sentinelSource, relativePath);
    }

    [Fact]
    public async Task SearchTextAsync_UnreadableFileInScope_ReturnsIncompleteWithRelativeLimitationNotCompleteEmpty()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        const string relativeDirectory = "partial-search";
        string searchRoot = Path.Combine(fixture.PreparedRootPath, relativeDirectory);
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(
            Path.Combine(searchRoot, "a-readable.oddx"),
            "needle in authorized file",
            new UTF8Encoding(false));
        const string lockedName = "z-locked.oddx";
        string lockedPath = Path.Combine(searchRoot, lockedName);
        const string sourceFragment = "unreadable private bytes";
        await File.WriteAllTextAsync(lockedPath, sourceFragment, new UTF8Encoding(false));
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        await using var exclusive = new FileStream(
            lockedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest(relativeDirectory, "absent", PageSize: 64));

        Assert.Equal(PreparedSourceOutcome.Incomplete, result.Outcome);
        Assert.Contains(
            result.Limitations,
            limitation => limitation.Location.RelativePath == relativeDirectory + "/" + lockedName &&
                limitation.Reason == PreparedSourceLimitationReason.Unreadable);
        AssertSanitized(result, fixture, sourceFragment, lockedPath, fixture.Approval.SubjectId,
            fixture.Approval.ApprovalIdentity, fixture.PreparedRootPath);
    }

    [Fact]
    public async Task SearchTextAsync_FullAuthorizedNoMatch_ReturnsCompleteEmpty()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string searchRoot = Path.Combine(fixture.PreparedRootPath, "fully-readable");
        Directory.CreateDirectory(searchRoot);
        await File.WriteAllTextAsync(
            Path.Combine(searchRoot, "readable.oddx"),
            "all authorized bytes are readable",
            new UTF8Encoding(false));
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.SearchTextAsync(
            new PreparedSourceSearchTextRequest("fully-readable", "not-present", PageSize: 64));

        Assert.Equal(PreparedSourceOutcome.CompleteEmpty, result.Outcome);
        Assert.Empty(result.Matches);
        Assert.Empty(result.Limitations);
        Assert.Null(result.Continuation);
    }

    private static void AssertSanitized(
        PreparedSourceResult result,
        PreparedSourceFixture fixture,
        params string[] sensitiveValues)
    {
        string json = JsonSerializer.Serialize(result);
        Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
        Assert.DoesNotContain(fixture.PreparedRootPath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.SiblingPath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Approval.SubjectId, json, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Approval.ApprovalIdentity, json, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Approval.RootBindingIdentity, json, StringComparison.Ordinal);
        foreach (string value in sensitiveValues)
        {
            Assert.DoesNotContain(value, json, StringComparison.Ordinal);
        }
    }

    private static void RequireWindowsHost()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip(
                "Windows handle-relative prepared-source access is not qualified on this platform.");
        }
    }

    private static async Task<IPreparedSourceReadContext> IssueContextAsync(
        PreparedSourceFixture fixture)
    {
        fixture.Authority.Approve(fixture.Approval);
        var issuer = new PreparedSourceContextIssuer(fixture.Authority);
        PreparedSourceIssueResult issue = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedSourceGrantLimits.Default));
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.Context);
        return issue.Context;
    }

}
