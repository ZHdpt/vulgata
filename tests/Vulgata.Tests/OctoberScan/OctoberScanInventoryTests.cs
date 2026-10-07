using Vulgata.Core.DomainServices;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanInventoryTests
{
    private static readonly Guid Repository = Guid.Parse("8d04bbc8-b0f5-4329-8254-853265acbc7c");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Build_LongestSegmentRuleWins_PriorityOnlyOrdersWorkAsync()
    {
        var calls = new List<string>();
        var inventory = Inventory(calls, new()
        {
            [""] = Page(Directory("src"), Directory("src2")),
            ["src"] = Page(File("src/a.cs"), Directory("src/private")),
            ["src/private"] = Page(File("src/private/secret.cs")),
            ["src2"] = Page(File("src2/b.cs")),
        });
        var result = await inventory.BuildAsync(Repository, "", [
            new(Repository, "", "supporting", "default supporting", 99),
            new(Repository, "src", "selected", "selected src", 3),
            new(Repository, "src/private", "excluded", "private subtree", -1)]);
        Assert.True(result.IsComplete);
        Assert.Empty(result.Gaps);
        Assert.Equal("selected", result.Entries.Single(item => item.RelativePath == "src/a.cs").Disposition);
        Assert.Equal("excluded", result.Entries.Single(item => item.RelativePath == "src/private/secret.cs").Disposition);
        Assert.Equal("supporting", result.Entries.Single(item => item.RelativePath == "src2/b.cs").Disposition);
        Assert.Equal(new[] { "src2/b.cs", "src/a.cs", "src/private/secret.cs" }, result.Entries.Select(item => item.RelativePath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Build_UnknownExtensionsAndVisibleSameNamedToolDirectory_RemainInExplicitScopeAsync()
    {
        var inventory = Inventory([], new()
        {
            [""] = Page(File("unknown.oddlangx"), Directory(".codegraph")),
            [".codegraph"] = Page(File(".codegraph/legitimate-source")),
        });
        var result = await inventory.BuildAsync(Repository, "", [new(Repository, "", "selected", "explicit all source")]);
        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Entries.Length);
        Assert.All(result.Entries, entry => { Assert.Equal("selected", entry.Disposition); Assert.Equal("explicit all source", entry.Reason); });
        Assert.Contains(result.Entries, entry => entry.RelativePath == "unknown.oddlangx");
        Assert.Contains(result.Entries, entry => entry.RelativePath == ".codegraph/legitimate-source");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Build_UnmatchedFile_IsExplicitlyExcludedWithoutWideningDeclaredDirectoryAsync()
    {
        var calls = new List<string>();
        var inventory = Inventory(calls, new() { ["src"] = Page(File("src/chosen.cs"), File("src/other.cs")) });
        var result = await inventory.BuildAsync(Repository, "src", [new(Repository, "src/chosen.cs", "selected", "one file")]);
        Assert.True(result.IsComplete);
        Assert.Equal(new[] { "src" }, calls);
        var unmatched = result.Entries.Single(entry => entry.RelativePath == "src/other.cs");
        Assert.Equal("excluded", unmatched.Disposition);
        Assert.Equal("outside-configured-source-rules", unmatched.Reason);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Build_NonTraversableLink_ReportsUnsupportedAndUnknownRegionWithoutFollowingAsync()
    {
        var calls = new List<string>();
        var inventory = Inventory(calls, new() { [""] = Page(new PreparedSourceEntry(new(Repository, "linked"), PreparedSourceEntryKind.NonTraversableLink)) });
        var result = await inventory.BuildAsync(Repository, "", [new(Repository, "", "selected", "explicit scope")]);
        Assert.False(result.IsComplete);
        var entry = Assert.Single(result.Entries);
        Assert.Equal("unsupported", entry.Disposition);
        Assert.Equal("non-traversable-link", entry.Reason);
        Assert.NotEmpty(result.Gaps);
        Assert.Equal(new[] { "" }, calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Build_IncompleteContainedTraversal_PreservesKnownEntriesAndUnknownRegionGapAsync()
    {
        var partial = Page(File("known.cs")) with { Outcome = PreparedSourceOutcome.Incomplete };
        var result = await Inventory([], new() { [""] = partial }).BuildAsync(Repository, "", [new(Repository, "", "selected", "explicit scope")]);
        Assert.False(result.IsComplete);
        Assert.Contains(result.Entries, entry => entry.RelativePath == "known.cs");
        Assert.Contains(result.Gaps, gap => gap.RepositoryId == Repository && gap.RelativeDirectory == "");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("entries")]
    [InlineData("directories")]
    [InlineData("depth")]
    public async Task Build_FiniteInventoryLimit_ReportsGapWithoutClaimingCompleteAsync(string limit)
    {
        var calls = new List<string>();
        var inventory = Inventory(calls, new()
        {
            [""] = Page(File("a.cs"), File("b.cs"), Directory("nested")),
            ["nested"] = Page(Directory("nested/deeper")),
            ["nested/deeper"] = Page(File("nested/deeper/c.cs")),
        });
        var limits = limit switch
        {
            "entries" => new OctoberScanInventoryLimits(MaximumEntries: 1),
            "directories" => new OctoberScanInventoryLimits(MaximumDirectories: 1),
            _ => new OctoberScanInventoryLimits(MaximumDepth: 1),
        };
        var result = await inventory.BuildAsync(Repository, "", [new(Repository, "", "selected", "explicit scope")], limits);
        Assert.False(result.IsComplete);
        Assert.NotEmpty(result.Gaps);
        if (limit == "entries") Assert.InRange(result.Entries.Length, 0, 1);
        if (limit == "directories") Assert.Single(calls);
        if (limit == "depth") Assert.DoesNotContain("nested/deeper", calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_ForeignRepositoryOrOutOfDirectoryEntry_CannotTraverseOrAdmitItAsync(bool foreignRepository)
    {
        var calls = new List<string>();
        var foreignPath = foreignRepository ? "src/foreign" : "src2/escape";
        var foreign = new PreparedSourceEntry(new(foreignRepository ? Guid.NewGuid() : Repository, foreignPath), PreparedSourceEntryKind.Directory);
        var inventory = Inventory(calls, new() { ["src"] = Page(File("src/known.cs"), foreign) });
        var result = await inventory.BuildAsync(Repository, "src", [new(Repository, "src", "selected", "declared src scope")]);
        Assert.False(result.IsComplete);
        Assert.NotEmpty(result.Gaps);
        Assert.DoesNotContain(result.Entries, entry => entry.RelativePath == foreignPath || entry.RepositoryId != Repository);
        Assert.All(result.Entries, entry => Assert.StartsWith("src/", entry.RelativePath, StringComparison.Ordinal));
        Assert.Equal(new[] { "src" }, calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(PreparedSourceOutcome.Denied)]
    [InlineData(PreparedSourceOutcome.AuthorityUnavailable)]
    [InlineData(PreparedSourceOutcome.ChangedInput)]
    public async Task Build_AuthorityOrIntegrityLostWithQueuedDirectories_DiscardsPreviouslyObservedNamesAsync(PreparedSourceOutcome outcome)
    {
        var calls = new List<string>();
        var inventory = Inventory(calls, new()
        {
            ["declared"] = Page(Directory("declared/confidential-a"), Directory("declared/confidential-b")),
            ["declared/confidential-a"] = new() { RepositoryId = Repository, Operation = PreparedSourceOperation.List, Outcome = outcome },
        });
        var result = await inventory.BuildAsync(Repository, "declared", [new(Repository, "declared", "selected", "explicit caller scope")]);
        Assert.False(result.IsComplete);
        Assert.Empty(result.Entries);
        Assert.All(result.Gaps, gap =>
        {
            Assert.Equal("declared", gap.RelativeDirectory);
            Assert.DoesNotContain("confidential-a", gap.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain("confidential-b", gap.Reason, StringComparison.Ordinal);
        });
        Assert.Equal(new[] { "declared", "declared/confidential-a" }, calls);
    }

    private static OctoberScanInventory Inventory(List<string> calls, Dictionary<string, PreparedSourceResult> pages) => new((repository, request, token) =>
    {
        Assert.Equal(Repository, repository);
        Assert.Null(request.Continuation); // Runtime supplies its already-drained bounded directory result.
        calls.Add(request.RelativeDirectory);
        return Task.FromResult(pages[request.RelativeDirectory]);
    });
    private static PreparedSourceEntry File(string path) => new(new(Repository, path), PreparedSourceEntryKind.RegularFile, 17);
    private static PreparedSourceEntry Directory(string path) => new(new(Repository, path), PreparedSourceEntryKind.Directory);
    private static PreparedSourceResult Page(params PreparedSourceEntry[] entries) => new()
    {
        RepositoryId = Repository, Operation = PreparedSourceOperation.List, Outcome = PreparedSourceOutcome.Complete, Entries = entries,
    };
}
