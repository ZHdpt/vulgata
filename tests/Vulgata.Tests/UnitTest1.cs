namespace Vulgata.Tests;

public class UnitTest1
{
    [Fact]
    public void ChatAndManagementPages_AreScaffolded()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vulgata.slnx")))
        {
            dir = dir.Parent;
        }
        var repoRoot = dir?.FullName
            ?? throw new InvalidOperationException("Repository root could not be found.");

        Assert.True(File.Exists(Path.Combine(repoRoot, "src", "dotnet", "Vulgata.Web", "Components", "Pages", "ChatPage.razor")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "src", "dotnet", "Vulgata.Web", "Components", "Pages", "Management", "DashboardPage.razor")));
    }
}
