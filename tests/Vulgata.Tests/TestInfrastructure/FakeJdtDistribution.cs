namespace Vulgata.Tests.TestInfrastructure;

/// <summary>File layout consumed by the adapter with a recording process launcher; never executable.</summary>
internal sealed class FakeJdtDistribution : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "vulgata-fake-jdt-" + Guid.NewGuid().ToString("N"));
    internal string Workspace => Path.Combine(Root, "workspace");

    internal FakeJdtDistribution()
    {
        Directory.CreateDirectory(Path.Combine(Root, "plugins"));
        Directory.CreateDirectory(Path.Combine(Root, "config_win"));
        File.WriteAllText(Path.Combine(Root, "plugins", "org.eclipse.equinox.launcher_test.jar"), "recording launcher fixture");
        File.WriteAllText(Path.Combine(Root, "config_win", "config.ini"), "osgi.bundles=fixture");
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
