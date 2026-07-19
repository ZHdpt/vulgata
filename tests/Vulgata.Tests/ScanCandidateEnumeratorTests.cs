using System.Diagnostics;
using Vulgata.Infrastructure.Git;

namespace Vulgata.Tests;

public sealed class ScanCandidateEnumeratorTests
{
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
    public async Task EnumerateAsync_WithLargeIgnoredInventory_ClassifiesEveryIgnoredPathAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-candidates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await RunGitAsync(root, "init");
            await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.generated.cs\n");
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
                    candidate.Category == "GitIgnore"));
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
            await RunGitAsync(root, "init");
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

    private static async Task RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await process.StandardError.ReadToEndAsync());
    }
}
