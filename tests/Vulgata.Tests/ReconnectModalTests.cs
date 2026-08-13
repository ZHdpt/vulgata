namespace Vulgata.Tests;

public sealed class ReconnectModalTests
{
    [Fact]
    public void FailedReconnect_DoesNotReloadImmediately()
    {
        string script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "dotnet",
            "Vulgata.Web",
            "Components",
            "Layout",
            "ReconnectModal.razor.js")).ReplaceLineEndings("\n");

        int failedBranch = script.IndexOf(
            "event.detail.state === \"failed\"",
            StringComparison.Ordinal);
        int rejectedBranch = script.IndexOf(
            "event.detail.state === \"rejected\"",
            StringComparison.Ordinal);
        int visibilityRetry = script.IndexOf(
            "document.addEventListener(\"visibilitychange\", retryWhenDocumentBecomesVisible);",
            failedBranch,
            StringComparison.Ordinal);
        int reload = script.IndexOf("location.reload();", failedBranch, StringComparison.Ordinal);

        Assert.True(failedBranch >= 0);
        Assert.True(rejectedBranch > failedBranch);
        Assert.InRange(visibilityRetry, failedBranch + 1, rejectedBranch - 1);
        Assert.True(reload < 0 || reload >= rejectedBranch);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
               throw new InvalidOperationException("Repository root could not be found.");
    }
}
