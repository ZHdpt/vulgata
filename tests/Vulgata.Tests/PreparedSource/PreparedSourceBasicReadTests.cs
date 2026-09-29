using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Xunit.Sdk;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedSourceBasicReadTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_Root_ReturnsImmediateRepositoryRelativeEntriesWithoutAdmissionDecisions()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string nestedDirectory = Path.Combine(fixture.PreparedRootPath, "nested");
        Directory.CreateDirectory(nestedDirectory);
        await File.WriteAllTextAsync(Path.Combine(nestedDirectory, "child.txt"), "nested content");
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest(RelativeDirectory: "", PageSize: 100));

        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal(PreparedSourceOperation.List, result.Operation);
        Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);

        PreparedSourceEntry unfamiliar = Assert.Single(
            result.Entries.Where(entry => entry.Location.RelativePath == "notes.oddlangx"));
        Assert.Equal(fixture.Approval.RepositoryId, unfamiliar.Location.RepositoryId);
        Assert.Equal(PreparedSourceEntryKind.RegularFile, unfamiliar.Kind);

        PreparedSourceEntry nested = Assert.Single(
            result.Entries.Where(entry => entry.Location.RelativePath == "nested"));
        Assert.Equal(PreparedSourceEntryKind.Directory, nested.Kind);
        Assert.DoesNotContain(result.Entries, entry => entry.Location.RelativePath == "nested/child.txt");

        string serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(fixture.PreparedRootPath, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SiblingPath, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("outside the approved prepared root", serialized, StringComparison.Ordinal);
        AssertNoAdmissionDecisionFields(serialized);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadTextRangeAsync_UnfamiliarExtension_ReturnsBoundedRepositoryRelativeText()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest(
                RelativePath: "notes.oddlangx",
                ByteOffset: 0,
                ByteCount: 5));

        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        Assert.Equal(PreparedSourceOperation.ReadTextRange, result.Operation);
        Assert.Equal(fixture.Approval.RepositoryId, result.RepositoryId);
        Assert.Equal("café", result.Text);
        Assert.Equal(
            new PreparedSourceReadLocation(
                fixture.Approval.RepositoryId,
                "notes.oddlangx",
                ByteStart: 0,
                ByteEndExclusive: 5),
            result.ReadLocation);
        if (result.ObservedFileByteLength is not null)
        {
            Assert.Equal(
                Encoding.UTF8.GetByteCount("café\nsource fixture\n"),
                result.ObservedFileByteLength);
        }
        Assert.Null(result.NextByteOffset);
        Assert.Empty(result.Entries);
        Assert.Empty(result.Matches);
        AssertNoAdmissionDecisionFields(JsonSerializer.Serialize(result));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListAsync_Root_ReportsEscapingLinkAsNonTraversableMetadata()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string linkPath = Path.Combine(fixture.PreparedRootPath, "outside-link.txt");
        CreateFileSymbolicLinkOrSkip(linkPath, fixture.SentinelPath);
        string sentinelBefore = await File.ReadAllTextAsync(fixture.SentinelPath);
        DateTime sentinelWriteTimeBefore = File.GetLastWriteTimeUtc(fixture.SentinelPath);
        await using IPreparedSourceReadContext context = await IssueContextAsync(fixture);

        PreparedSourceResult result = await context.ListAsync(
            new PreparedSourceListRequest(RelativeDirectory: "", PageSize: 100));

        Assert.Equal(PreparedSourceOutcome.Complete, result.Outcome);
        PreparedSourceEntry link = Assert.Single(
            result.Entries.Where(entry => entry.Location.RelativePath == "outside-link.txt"));
        Assert.Equal(fixture.Approval.RepositoryId, link.Location.RepositoryId);
        Assert.Equal(PreparedSourceEntryKind.NonTraversableLink, link.Kind);
        Assert.Contains(
            result.Limitations,
            limitation => limitation.Location == link.Location &&
                limitation.Reason == PreparedSourceLimitationReason.NonTraversableLink);

        string serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(fixture.SentinelPath, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinelBefore, serialized, StringComparison.Ordinal);
        Assert.Equal(sentinelBefore, await File.ReadAllTextAsync(fixture.SentinelPath));
        Assert.Equal(sentinelWriteTimeBefore, File.GetLastWriteTimeUtc(fixture.SentinelPath));
        AssertNoAdmissionDecisionFields(serialized);
    }

    private static void AssertNoAdmissionDecisionFields(string serialized)
    {
        using JsonDocument document = JsonDocument.Parse(serialized);
        AssertNoAdmissionDecisionFields(document.RootElement);
    }

    private static void AssertNoAdmissionDecisionFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string name = property.Name;
                Assert.False(
                    name.Contains("admission", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("support", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("exclusion", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("excluded", StringComparison.OrdinalIgnoreCase),
                    $"Listing exposed an admission/support/exclusion decision field: {name}");
                AssertNoAdmissionDecisionFields(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                AssertNoAdmissionDecisionFields(item);
            }
        }
    }

    private static void RequireWindowsHost()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Windows handle-relative prepared-source access is not qualified on this platform.");
        }
    }

    private static void CreateFileSymbolicLinkOrSkip(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (UnauthorizedAccessException)
        {
            throw SkipException.ForSkip(
                "File symbolic-link setup requires privilege that is unavailable on this Windows host.");
        }
        catch (PlatformNotSupportedException)
        {
            throw SkipException.ForSkip("File symbolic links are unsupported on this Windows host.");
        }
        catch (IOException exception) when (
            exception.HResult is unchecked((int)0x80070522) or unchecked((int)0x80070005))
        {
            throw SkipException.ForSkip(
                "File symbolic-link privilege is unavailable on this Windows host.");
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
