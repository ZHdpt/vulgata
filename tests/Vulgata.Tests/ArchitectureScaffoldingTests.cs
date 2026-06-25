using Vulgata.Shared;

namespace Vulgata.Tests;

public class ArchitectureScaffoldingTests
{
    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vulgata.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("Repository root could not be found. Ensure Vulgata.slnx exists at the solution root.");
    }

    [Fact]
    public void LoadState_ContainsAllExpectedValues()
    {
        var names = Enum.GetNames<LoadState>();

        Assert.Contains(nameof(LoadState.Idle), names);
        Assert.Contains(nameof(LoadState.Loading), names);
        Assert.Contains(nameof(LoadState.Loaded), names);
        Assert.Contains(nameof(LoadState.Refreshing), names);
        Assert.Contains(nameof(LoadState.Empty), names);
        Assert.Contains(nameof(LoadState.NoResults), names);
        Assert.Contains(nameof(LoadState.Error), names);
        Assert.Contains(nameof(LoadState.Cancelling), names);
    }

    [Fact]
    public void PlaceholderLanguageDirectories_Exist()
    {
        var repoRoot = GetRepoRoot();

        Assert.True(Directory.Exists(Path.Combine(repoRoot, "src", "java")));
        Assert.True(Directory.Exists(Path.Combine(repoRoot, "src", "python")));
        Assert.True(Directory.Exists(Path.Combine(repoRoot, "src", "node")));
    }

    [Fact]
    public void DockerCompose_ExistsAtRepoRoot()
    {
        var repoRoot = GetRepoRoot();
        var composePath = Path.Combine(repoRoot, "docker-compose.yml");

        Assert.True(File.Exists(composePath));
    }
}
