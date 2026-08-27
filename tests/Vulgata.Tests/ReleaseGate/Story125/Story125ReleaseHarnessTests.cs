using System.Diagnostics;

namespace Vulgata.Tests.ReleaseGate;

public sealed class Story125ReleaseHarnessTests
{
    private const string ProvisionalBaseCommit =
        "0d835c5f65f70a3c6a9561c6ff600069bb93e832";

    [Fact]
    public void Story125Boundary_NoMigrationDbContextStory124OrUncoordinatedSharedDelta_RemainsIntact()
    {
        string repositoryRoot = FindRepositoryRoot();
        IReadOnlyList<string> changedPaths = GetChangedPaths(repositoryRoot);

        string[] forbiddenPaths =
        [
            "src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs",
            "src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs",
        ];

        Assert.DoesNotContain(
            changedPaths,
            path => forbiddenPaths.Contains(path, StringComparer.Ordinal));
        Assert.DoesNotContain(
            changedPaths,
            path => path.StartsWith(
                "src/dotnet/Vulgata.Infrastructure/Data/Migrations/",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            changedPaths,
            path => path.StartsWith(
                "specs/013-exact-cross-repository-flow-pass2/",
                StringComparison.Ordinal));

        string ownershipManifest = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "specs",
            "014-grounded-llm-wiki-chat-release-evidence",
            "evidence",
            "ownership",
            "README.md"));
        Assert.Contains(
            "src/dotnet/Vulgata.Web/Program.cs",
            ownershipManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "one root-coordinated owner",
            ownershipManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "Track A may",
            ownershipManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "accepted T019 Scan status registrations",
            ownershipManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "Agents DI remains outside Track A",
            ownershipManifest,
            StringComparison.Ordinal);

        string[] uncoordinatedSharedIntegrationPaths =
        [
            "src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs",
            "src/dotnet/Vulgata.Agents/DependencyInjection.cs",
            "Directory.Packages.props",
            "Directory.Build.props",
        ];
        Assert.DoesNotContain(
            changedPaths,
            path => path is not null &&
                (uncoordinatedSharedIntegrationPaths.Contains(path, StringComparer.Ordinal) ||
                    path.EndsWith(".csproj", StringComparison.Ordinal)));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException(
                "The Vulgata repository root could not be located.");
    }

    private static IReadOnlyList<string> GetChangedPaths(string repositoryRoot)
    {
        string committedAndWorkingTree = RunGit(
            repositoryRoot,
            "diff",
            "--name-only",
            "--no-renames",
            ProvisionalBaseCommit,
            "--");
        string untracked = RunGit(
            repositoryRoot,
            "ls-files",
            "--others",
            "--exclude-standard");

        return committedAndWorkingTree
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Concat(untracked.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
            .Select(path => path.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string RunGit(string repositoryRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to start git.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed: {error}");
        return output;
    }
}
