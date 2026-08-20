using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Vulgata.Infrastructure.CommunicationDetection;

namespace Vulgata.Tests.PreparedScanning;

public sealed class PreparedScanProductionLiteralQualificationTests
{
    private const string _artifactRelativePath =
        "artifacts/qualification/story-12.2/production-literal-inspection.json";

    [Fact]
    public void ProductionArtifacts_FixtureDerivedDenylist_HasZeroExactMatches()
    {
        string repositoryRoot = FindRepositoryRoot();
        IReadOnlyList<DenylistEntry> denylist = BuildDenylist(repositoryRoot);
        string denylistSha256 = HashDenylist(denylist);
        var matches = new List<LiteralMatch>();

        IReadOnlyList<InspectedAssembly> assemblies = InspectAssemblies(denylist, matches);
        IReadOnlyList<InspectedResource> resources = InspectResources(assemblies, denylist, matches);
        IReadOnlyList<InspectedPublicOutput> publicOutputs = InspectPublicOutputs(denylist, matches);

        var artifact = new QualificationArtifact(
            "story-12.2-production-literal-inspection/1.0",
            "T049",
            denylistSha256,
            denylist,
            assemblies.Select(item => item.Metadata).ToArray(),
            resources,
            publicOutputs,
            [
                "Vulgata.Tests.dll and every other test/fixture binary",
                "tests/Vulgata.Tests/Fixtures/**",
                "third-party and platform assemblies",
            ],
            matches.OrderBy(match => match.ArtifactIdentity, StringComparer.Ordinal)
                .ThenBy(match => match.Literal, StringComparer.Ordinal)
                .ToArray(),
            matches.Count == 0 ? "zero-match" : "fixture-literal-match");

        WriteArtifact(repositoryRoot, artifact);

        Assert.True(
            matches.Count == 0,
            $"Fixture-derived production literals were found: {JsonSerializer.Serialize(matches)}");
    }

    private static IReadOnlyList<DenylistEntry> BuildDenylist(string repositoryRoot)
    {
        string fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "Vulgata.Tests",
            "Fixtures",
            "PreparedScanning");
        string manifestPath = Path.Combine(fixtureRoot, "fixture-manifest.json");
        string snapshotPath = Path.Combine(fixtureRoot, "snapshot-inventory.json");
        string catalogPath = Path.Combine(fixtureRoot, "catalog", "all-grammar.json");
        string xmlPath = Path.Combine(
            fixtureRoot,
            "repository",
            "src",
            "main",
            "resources",
            "neutral",
            "communication.xml");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        using JsonDocument snapshot = JsonDocument.Parse(File.ReadAllBytes(snapshotPath));
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllBytes(catalogPath));
        var entries = new List<DenylistEntry>();

        string catalogId = catalog.RootElement.GetProperty("catalogId").GetString()!;
        entries.Add(new("repository-name", catalogId, Relative(repositoryRoot, catalogPath)));

        string requestedRef = snapshot.RootElement.GetProperty("requestedRef").GetString()!;
        entries.Add(new("repository-ref", requestedRef, Relative(repositoryRoot, snapshotPath)));
        entries.Add(new("repository-name", requestedRef[(requestedRef.LastIndexOf('/') + 1)..],
            Relative(repositoryRoot, snapshotPath)));

        foreach (JsonElement entry in snapshot.RootElement.GetProperty("entries").EnumerateArray())
        {
            string path = entry.GetProperty("path").GetString()!;
            entries.Add(new("canonical-path", path, Relative(repositoryRoot, snapshotPath)));
        }

        foreach (JsonElement safePath in manifest.RootElement.GetProperty("safeProductionPaths").EnumerateArray())
        {
            entries.Add(new(
                "fixture-rooted-path",
                safePath.GetString()!,
                Relative(repositoryRoot, manifestPath)));
        }

        using (XmlReader reader = XmlReader.Create(
            xmlPath,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            while (reader.Read())
            {
                string? endpoint = reader.GetAttribute("endpoint");
                if (endpoint is null)
                {
                    continue;
                }

                entries.Add(new("endpoint", endpoint, Relative(repositoryRoot, xmlPath)));
                const string scheme = "svc://";
                if (endpoint.StartsWith(scheme, StringComparison.Ordinal))
                {
                    entries.Add(new("endpoint-after-prefix-removal", endpoint[scheme.Length..],
                        Relative(repositoryRoot, xmlPath)));
                }
            }
        }

        int[] fixtureLineValues = manifest.RootElement.GetProperty("splitVectors")
            .EnumerateArray()
            .SelectMany(vector => vector.GetProperty("ranges").EnumerateArray())
            .SelectMany(range => range.EnumerateArray())
            .Select(value => value.GetInt32())
            .Where(value => value > 0)
            .Distinct()
            .Order()
            .ToArray();
        foreach (int line in fixtureLineValues)
        {
            foreach (string literal in LineNumberContexts(line))
            {
                entries.Add(new("fixture-line-context", literal, Relative(repositoryRoot, manifestPath)));
            }
        }

        string apiContract = Path.Combine(
            repositoryRoot,
            "specs",
            "011-java-xml-prescan-manifest",
            "contracts",
            "preparation-api.md");
        entries.Add(new("release-repository-ref", "refs/tags/release-a", Relative(repositoryRoot, apiContract)));
        entries.Add(new("release-repository-ref", "refs/tags/release-b", Relative(repositoryRoot, apiContract)));
        entries.Add(new("release-repository-name", "release-a", Relative(repositoryRoot, apiContract)));
        entries.Add(new("release-repository-name", "release-b", Relative(repositoryRoot, apiContract)));

        return entries
            .Distinct()
            .OrderBy(entry => entry.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.Literal, StringComparer.Ordinal)
            .ThenBy(entry => entry.Source, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> LineNumberContexts(int line)
    {
        string value = line.ToString(System.Globalization.CultureInfo.InvariantCulture);
        yield return $"\"line\":{value}";
        yield return $"\"lineNumber\":{value}";
        yield return $"\"lineStart\":{value}";
        yield return $"\"lineEnd\":{value}";
        yield return $"line={value}";
        yield return $"line:{value}";
    }

    private static IReadOnlyList<InspectedAssembly> InspectAssemblies(
        IReadOnlyList<DenylistEntry> denylist,
        ICollection<LiteralMatch> matches)
    {
        string[] assemblyPaths = Directory.GetFiles(AppContext.BaseDirectory, "Vulgata.*.dll")
            .Where(path => !Path.GetFileName(path).StartsWith("Vulgata.Tests", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var result = new List<InspectedAssembly>(assemblyPaths.Length);
        foreach (string path in assemblyPaths)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assembly assembly = Assembly.LoadFrom(path);
            string identity = Path.GetFileName(path);
            FindMatches("assembly", identity, bytes, denylist, matches, includeUtf16: true);
            result.Add(new(
                new(
                    identity,
                    assembly.GetName().Version?.ToString() ?? "0.0.0.0",
                    assembly.ManifestModule.ModuleVersionId.ToString("D"),
                    Convert.ToHexStringLower(SHA256.HashData(bytes))),
                assembly));
        }

        Assert.NotEmpty(result);
        return result;
    }

    private static IReadOnlyList<InspectedResource> InspectResources(
        IReadOnlyList<InspectedAssembly> assemblies,
        IReadOnlyList<DenylistEntry> denylist,
        ICollection<LiteralMatch> matches)
    {
        var result = new List<InspectedResource>();
        foreach (InspectedAssembly inspected in assemblies)
        {
            foreach (string resourceName in inspected.Assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
            {
                using Stream stream = inspected.Assembly.GetManifestResourceStream(resourceName)!;
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                byte[] bytes = buffer.ToArray();
                string identity = $"{inspected.Metadata.Identity}::{resourceName}";
                FindMatches("resource", identity, bytes, denylist, matches, includeUtf16: true);
                result.Add(new(
                    identity,
                    bytes.LongLength,
                    Convert.ToHexStringLower(SHA256.HashData(bytes))));
            }
        }

        return result;
    }

    private static IReadOnlyList<InspectedPublicOutput> InspectPublicOutputs(
        IReadOnlyList<DenylistEntry> denylist,
        ICollection<LiteralMatch> matches)
    {
        CommunicationPatternCatalog catalog = CommunicationCatalogLoader.LoadEmbedded();
        byte[] catalogOutput = JsonSerializer.SerializeToUtf8Bytes(new
        {
            catalog.SchemaIdentity,
            catalog.CatalogId,
            catalog.Version,
            catalog.CatalogSha256,
            Rules = catalog.Rules.Select(rule => new
            {
                rule.Identity,
                rule.PathGlob,
                rule.Selector,
                rule.Ancestor,
                rule.AttributeTests,
                rule.Extraction,
                rule.RemovePrefix,
                CommunicationKind = rule.CommunicationKind.ToString(),
                Rule = rule.Rule.ToString(),
                Provenance = rule.Provenance.ToString(),
            }),
        });
        const string identity = "CommunicationCatalogLoader.LoadEmbedded";
        FindMatches("public-output", identity, catalogOutput, denylist, matches, includeUtf16: false);
        return
        [
            new(identity, catalogOutput.LongLength, Convert.ToHexStringLower(SHA256.HashData(catalogOutput))),
        ];
    }

    private static void FindMatches(
        string artifactKind,
        string artifactIdentity,
        byte[] content,
        IReadOnlyList<DenylistEntry> denylist,
        ICollection<LiteralMatch> matches,
        bool includeUtf16)
    {
        foreach (DenylistEntry entry in denylist)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(entry.Literal);
            if (content.AsSpan().IndexOf(utf8) >= 0)
            {
                matches.Add(new(artifactKind, artifactIdentity, entry.Category, entry.Literal, "utf-8"));
            }

            if (includeUtf16)
            {
                byte[] utf16 = Encoding.Unicode.GetBytes(entry.Literal);
                if (content.AsSpan().IndexOf(utf16) >= 0)
                {
                    matches.Add(new(artifactKind, artifactIdentity, entry.Category, entry.Literal, "utf-16le"));
                }
            }
        }
    }

    private static string HashDenylist(IReadOnlyList<DenylistEntry> denylist)
    {
        string canonical = string.Join(
            '\n',
            denylist.Select(entry => $"{entry.Category}\0{entry.Literal}\0{entry.Source}")) + "\n";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void WriteArtifact(string repositoryRoot, QualificationArtifact artifact)
    {
        string path = Path.Combine(repositoryRoot, _artifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            artifact,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllBytes(path, [.. json, (byte)'\n']);
    }

    private static string Relative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Vulgata repository root.");
    }

    private sealed record DenylistEntry(string Category, string Literal, string Source);

    private sealed record LiteralMatch(
        string ArtifactKind,
        string ArtifactIdentity,
        string Category,
        string Literal,
        string Encoding);

    private sealed record InspectedAssemblyIdentity(
        string Identity,
        string Version,
        string ModuleVersionId,
        string Sha256);

    private sealed record InspectedAssembly(InspectedAssemblyIdentity Metadata, Assembly Assembly);

    private sealed record InspectedResource(string Identity, long Bytes, string Sha256);

    private sealed record InspectedPublicOutput(string Identity, long Bytes, string Sha256);

    private sealed record QualificationArtifact(
        string SchemaVersion,
        string Task,
        string DenylistSha256,
        IReadOnlyList<DenylistEntry> Denylist,
        IReadOnlyList<InspectedAssemblyIdentity> InspectedAssemblies,
        IReadOnlyList<InspectedResource> InspectedResources,
        IReadOnlyList<InspectedPublicOutput> InspectedPublicOutputs,
        IReadOnlyList<string> Exclusions,
        IReadOnlyList<LiteralMatch> Matches,
        string Verdict);
}
