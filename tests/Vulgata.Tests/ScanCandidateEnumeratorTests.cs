using Vulgata.Infrastructure.Git;

namespace Vulgata.Tests;

public sealed class ScanCandidateEnumeratorTests
{
    [Fact]
    public async Task EnumerateAsync_WithMaterializedArchiveOutsideGit_EnumeratesDeterministicFilteredGroupsAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-archive-candidates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Assert.DoesNotContain(
                EnumeratePathHierarchy(root),
                path => Directory.Exists(Path.Combine(path, ".git")));
            await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "eligible/*.cs\n");
            string eligibleDirectory = Path.Combine(root, "eligible");
            Directory.CreateDirectory(eligibleDirectory);
            for (int index = 0; index <= 1_000; index++)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(eligibleDirectory, $"{index:D4}.cs"),
                    "public sealed class Candidate;");
            }

            string vendorDirectory = Path.Combine(root, "vendor");
            Directory.CreateDirectory(vendorDirectory);
            await File.WriteAllTextAsync(Path.Combine(vendorDirectory, "Dependency.cs"), "vendored");
            await File.WriteAllTextAsync(Path.Combine(root, "Generated.g.cs"), "generated");
            await File.WriteAllTextAsync(Path.Combine(root, "bundle.min.js"), "minified");
            await File.WriteAllBytesAsync(Path.Combine(root, "payload.bin"), [1, 0, 2]);
            var enumerator = new ScanCandidateEnumerator();

            IReadOnlyList<Vulgata.Core.DomainServices.ScanSourceCandidate> candidates =
                await enumerator.EnumerateAsync(root);

            Assert.Equal(
                candidates.Select(candidate => candidate.RelativePath).Order(StringComparer.Ordinal),
                candidates.Select(candidate => candidate.RelativePath));
            AssertCandidate(candidates, "eligible/0000.cs", isFiltered: false, "Eligible", group: 0);
            AssertCandidate(candidates, "eligible/0998.cs", isFiltered: false, "Eligible", group: 0);
            AssertCandidate(candidates, "eligible/0999.cs", isFiltered: false, "Eligible", group: 1);
            AssertCandidate(candidates, "eligible/1000.cs", isFiltered: false, "Eligible", group: 1);
            AssertCandidate(candidates, "vendor/Dependency.cs", isFiltered: true, "Vendored", group: 0);
            AssertCandidate(candidates, "Generated.g.cs", isFiltered: true, "Generated", group: 0);
            AssertCandidate(candidates, "bundle.min.js", isFiltered: true, "MinifiedJavaScript", group: 0);
            AssertCandidate(candidates, "payload.bin", isFiltered: true, "Binary", group: 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SanitizeDiagnostic_WithCredentialAndTokenShapes_ReturnsChineseSecretSafeReason()
    {
        const string diagnostic =
            "fatal: https://alice:supersecret@example.test/repo.git Authorization: Bearer abc123 token=xyz password=hunter2";

        string sanitized = GitWorkingCopyService.SanitizeDiagnostic(diagnostic);

        Assert.Equal("Git 命令执行失败，请检查仓库地址和访问权限。", sanitized);
        Assert.DoesNotContain("supersecret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnumerateAsync_WithLargeGeneratedArchiveInventory_ClassifiesEveryGeneratedPathAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-candidates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            for (int index = 0; index < 300; index++)
            {
                string name = $"{index:D4}-{new string('x', 32)}.generated.cs";
                await File.WriteAllTextAsync(Path.Combine(root, name), "generated");
            }

            var enumerator = new ScanCandidateEnumerator();

            IReadOnlyList<Vulgata.Core.DomainServices.ScanSourceCandidate> candidates =
                await enumerator.EnumerateAsync(root);

            Assert.Equal(
                300,
                candidates.Count(candidate =>
                    candidate.RelativePath.EndsWith(".generated.cs", StringComparison.Ordinal) &&
                    candidate.IsFiltered &&
                    candidate.Category == "Generated"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnumerateAsync_WithSymbolicLinkOutsideRoot_RejectsCandidateWithoutReadingTargetAsync()
    {
        string parent = Path.Combine(Path.GetTempPath(), $"vulgata-candidates-{Guid.NewGuid():N}");
        string root = Path.Combine(parent, "repository");
        Directory.CreateDirectory(root);
        try
        {
            string outsidePath = Path.Combine(parent, "outside.cs");
            await File.WriteAllTextAsync(outsidePath, "outside");
            File.CreateSymbolicLink(Path.Combine(root, "linked.cs"), outsidePath);
            var enumerator = new ScanCandidateEnumerator();

            IReadOnlyList<Vulgata.Core.DomainServices.ScanSourceCandidate> candidates =
                await enumerator.EnumerateAsync(root);

            Vulgata.Core.DomainServices.ScanSourceCandidate candidate =
                Assert.Single(candidates, item => item.RelativePath == "linked.cs");
            Assert.True(candidate.IsFiltered);
            Assert.Equal("ReparsePoint", candidate.Category);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private static IEnumerable<string> EnumeratePathHierarchy(string path)
    {
        for (DirectoryInfo? directory = new(path); directory is not null; directory = directory.Parent)
        {
            yield return directory.FullName;
        }
    }

    private static void AssertCandidate(
        IReadOnlyList<Vulgata.Core.DomainServices.ScanSourceCandidate> candidates,
        string relativePath,
        bool isFiltered,
        string category,
        int group)
    {
        Vulgata.Core.DomainServices.ScanSourceCandidate candidate =
            Assert.Single(candidates, item => item.RelativePath == relativePath);
        Assert.Equal(isFiltered, candidate.IsFiltered);
        Assert.Equal(category, candidate.Category);
        Assert.Equal(group, candidate.AuditGroupOrdinal);
    }
}
