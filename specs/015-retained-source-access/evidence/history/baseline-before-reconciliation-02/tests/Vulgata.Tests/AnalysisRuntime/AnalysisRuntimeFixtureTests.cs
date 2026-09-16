using System.Security.Cryptography;
using System.Text.Json;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class AnalysisRuntimeFixtureTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Manifest_NeutralRepositoryRevision_HasDeterministicContainedFilesAndRelationships()
    {
        string fixtureRoot = FindFixtureRoot();
        FixtureManifest manifest = ReadManifest(fixtureRoot);
        string repositoryRoot = ResolveContainedPath(fixtureRoot, manifest.SourceRoot);

        Assert.Equal("analysis-runtime-fixture/1.0", manifest.SchemaIdentity);
        Assert.Equal(Guid.Parse("12000000-0000-4000-8000-000000000002"), manifest.RepositoryId);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", manifest.ProfiledRevision.ObjectId);
        Assert.Equal(DateTimeOffset.Parse("2026-08-30T00:00:00Z"), manifest.ProfiledRevision.CommittedAtUtc);
        Assert.Equal(manifest.Files.OrderBy(file => file.Path, StringComparer.Ordinal), manifest.Files);

        foreach (FixtureFile file in manifest.Files)
        {
            string absolutePath = ResolveContainedPath(repositoryRoot, file.Path);
            Assert.True(File.Exists(absolutePath), $"Fixture file is missing: {file.Path}");
            using FileStream stream = File.OpenRead(absolutePath);
            Assert.Equal(file.Sha256, Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
        }

        Assert.Equal(
            new[] { "definition", "implementation", "reference", "call" },
            manifest.ExpectedRelationships.Select(relationship => relationship.Kind));
        Assert.All(
            manifest.ExpectedRelationships,
            relationship =>
            {
                Assert.StartsWith("example.analysis.", relationship.FromSymbol, StringComparison.Ordinal);
                Assert.StartsWith("example.analysis.", relationship.ToSymbol, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void ResolveContainedPath_TraversalOrRootedPath_RejectsEscapeFromFixtureRoot()
    {
        string fixtureRoot = FindFixtureRoot();
        string rootedOutsidePath = Path.Combine(
            Path.GetPathRoot(fixtureRoot) ?? throw new InvalidOperationException("Fixture root has no path root."),
            "outside-analysis-fixture.java");

        Assert.Throws<InvalidOperationException>(() => ResolveContainedPath(fixtureRoot, "../outside.java"));
        Assert.Throws<InvalidOperationException>(() => ResolveContainedPath(fixtureRoot, "repository/../../outside.java"));
        Assert.Throws<InvalidOperationException>(() => ResolveContainedPath(fixtureRoot, rootedOutsidePath));
    }

    private static FixtureManifest ReadManifest(string fixtureRoot)
    {
        string manifestPath = ResolveContainedPath(fixtureRoot, "fixture-manifest.json");
        FixtureManifest? manifest = JsonSerializer.Deserialize<FixtureManifest>(
            File.ReadAllText(manifestPath),
            _jsonOptions);

        return manifest ?? throw new InvalidOperationException("Fixture manifest is empty.");
    }

    private static string FindFixtureRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the Vulgata repository root.");
        }

        return Path.GetFullPath(Path.Combine(
            directory.FullName,
            "tests",
            "Vulgata.Tests",
            "Fixtures",
            "AnalysisRuntime"));
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Fixture paths must be non-empty relative paths.");
        }

        string canonicalRoot = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(Path.Combine(canonicalRoot, relativePath));
        string rootPrefix = canonicalRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!candidate.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidOperationException("Fixture path escapes the fixture root.");
        }

        return candidate;
    }

    public sealed record FixtureManifest(
        string SchemaIdentity,
        Guid RepositoryId,
        ProfiledRevisionMetadata ProfiledRevision,
        string SourceRoot,
        IReadOnlyList<FixtureFile> Files,
        IReadOnlyList<ExpectedRelationship> ExpectedRelationships);

    public sealed record ProfiledRevisionMetadata(string ObjectId, DateTimeOffset CommittedAtUtc);

    public sealed record FixtureFile(string Path, string Sha256);

    public sealed record ExpectedRelationship(string Kind, string FromSymbol, string ToSymbol);
}
