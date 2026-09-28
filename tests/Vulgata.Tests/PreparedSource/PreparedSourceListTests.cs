using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Tests.PreparedSource;

public sealed class PreparedSourceListTests
{
    [Fact]
    public async Task ListAsync_PageSizeOne_ContinuesInOrdinalOrderAndRejectsReplay()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string nestedDirectory = Path.Combine(fixture.PreparedRootPath, "nested");
        Directory.CreateDirectory(nestedDirectory);
        await File.WriteAllTextAsync(Path.Combine(nestedDirectory, "child.datax"), "not an immediate child");
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        string[] expectedPaths =
        [
            "empty.datax",
            "invalid.bytesx",
            "long.payloadx",
            "nested",
            "notes.oddlangx",
        ];
        var actualPaths = new List<string>();
        var issuedContinuations = new List<string>();
        string? continuation = null;

        for (int pageIndex = 0; pageIndex < expectedPaths.Length; pageIndex++)
        {
            PreparedSourceResult page = await context.ListAsync(
                new PreparedSourceListRequest("", PageSize: 1, Continuation: continuation));

            Assert.Equal(fixture.Approval.RepositoryId, page.RepositoryId);
            Assert.Equal(PreparedSourceOperation.List, page.Operation);
            PreparedSourceEntry entry = Assert.Single(page.Entries);
            actualPaths.Add(entry.Location.RelativePath);

            if (pageIndex < expectedPaths.Length - 1)
            {
                Assert.Equal(PreparedSourceOutcome.Limited, page.Outcome);
                Assert.NotNull(page.Continuation);
                issuedContinuations.Add(page.Continuation);
                continuation = page.Continuation;
            }
            else
            {
                Assert.Equal(PreparedSourceOutcome.Complete, page.Outcome);
                Assert.Null(page.Continuation);
            }
        }

        Assert.Equal(expectedPaths, actualPaths);
        Assert.DoesNotContain("nested/child.datax", actualPaths);
        PreparedSourceResult replay = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 1, Continuation: issuedContinuations[0]));

        Assert.Equal(PreparedSourceOutcome.Denied, replay.Outcome);
        Assert.Empty(replay.Entries);
        Assert.Null(replay.Continuation);
    }

    [Fact]
    public async Task ListAsync_ContinuationBoundToOriginalPageSize_IsDeniedForChangedScope()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        PreparedSourceResult firstPage = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 1));
        Assert.Equal(PreparedSourceOutcome.Limited, firstPage.Outcome);
        Assert.NotNull(firstPage.Continuation);

        PreparedSourceResult changedScope = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 2, firstPage.Continuation));

        Assert.Equal(PreparedSourceOutcome.Denied, changedScope.Outcome);
        Assert.Empty(changedScope.Entries);
        Assert.Null(changedScope.Continuation);
    }

    [Fact]
    public async Task ListAsync_PageSizeOneHundred_ReturnsAllImmediateChildrenAsComplete()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 100));

        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal(
            ["empty.datax", "invalid.bytesx", "long.payloadx", "notes.oddlangx"],
            result.Entries.Select(entry => entry.Location.RelativePath));
        Assert.All(result.Entries, entry => Assert.Equal(fixture.Approval.RepositoryId, entry.Location.RepositoryId));
        Assert.Null(result.Continuation);
        Assert.Null(result.StoppingLimit);
    }

    [Fact]
    public async Task ListAsync_EmptyRoot_ReturnsCompleteEmpty()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        foreach (string file in Directory.EnumerateFiles(fixture.PreparedRootPath))
        {
            File.Delete(file);
        }

        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 100));

        Assert.Equal(PreparedSourceOutcome.CompleteEmpty, result.Outcome);
        Assert.Empty(result.Entries);
        Assert.Empty(result.Limitations);
        Assert.Null(result.Continuation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ListAsync_PageSizeOutsideContract_IsDeniedWithoutEntries(int pageSize)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest("", pageSize));

        Assert.Equal(PreparedSourceOutcome.Denied, result.Outcome);
        Assert.Empty(result.Entries);
        Assert.Null(result.Continuation);
    }

    [Fact]
    public async Task ListAsync_ObservedEntryChangesBeforeContinuation_ReturnsChangedInput()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);
        PreparedSourceResult firstPage = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 1));
        Assert.Equal(PreparedSourceOutcome.Limited, firstPage.Outcome);
        Assert.Single(firstPage.Entries);
        Assert.NotNull(firstPage.Continuation);

        // The first ordinal entry was observed on page one. Replacing its content
        // changes the live source represented by that continuation.
        await File.WriteAllTextAsync(fixture.EmptyTextPath, "changed after listing");

        PreparedSourceResult nextPage = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 1, firstPage.Continuation));

        Assert.Equal(PreparedSourceOutcome.ChangedInput, nextPage.Outcome);
        Assert.Empty(nextPage.Entries);
        Assert.Null(nextPage.Continuation);
    }

    [Fact]
    public async Task ListAsync_MetadataBudgetExhaustion_IsExplicitAndDoesNotIssueContinuation()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantLimits limits = PreparedSourceGrantLimits.Default with
        {
            MaximumMetadataEntries = 1,
        };
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, limits);

        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 100));

        Assert.Equal(PreparedSourceOutcome.Limited, result.Outcome);
        Assert.Equal(PreparedSourceStoppingLimit.GrantMetadataEntries, result.StoppingLimit);
        Assert.InRange(result.Entries.Count, 0, 1);
        Assert.Null(result.Continuation);
    }

    [Fact]
    public async Task ListAsync_OperationBudgetExhaustion_IsExplicitOnLaterCall()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceGrantLimits limits = PreparedSourceGrantLimits.Default with
        {
            MaximumOperations = 1,
        };
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture, limits);

        PreparedSourceResult firstPage = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 1));
        Assert.Equal(PreparedSourceOutcome.Limited, firstPage.Outcome);
        Assert.NotNull(firstPage.Continuation);

        PreparedSourceResult nextPage = await context.ListAsync(
            new PreparedSourceListRequest("", PageSize: 1, firstPage.Continuation));

        Assert.Equal(PreparedSourceOutcome.Limited, nextPage.Outcome);
        Assert.Equal(PreparedSourceStoppingLimit.GrantOperations, nextPage.StoppingLimit);
        Assert.Empty(nextPage.Entries);
        Assert.Null(nextPage.Continuation);
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
