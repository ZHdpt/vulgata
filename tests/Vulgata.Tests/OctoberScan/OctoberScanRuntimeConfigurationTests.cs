using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanRuntimeConfigurationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExplicitRuntimeDocument_ParsesExactBindingsWithoutProbingSourceOrLaunchingToolsAsync()
    {
        using var file = new Document();
        var expected = file.Valid();
        file.Write(JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var actual = await OctoberGeneratorEntryPoint.ReadRuntimeConfigurationAsync(file.Configuration());
        Assert.Equal(expected.PreparedRoots.ToArray(), actual.PreparedRoots.ToArray());
        Assert.Equal(expected.CodeGraphCommandPath, actual.CodeGraphCommandPath);
        Assert.Equal(expected.JavaNavigation, actual.JavaNavigation);
        Assert.Equal(expected.LogDirectory, actual.LogDirectory);
        Assert.False(Directory.Exists(expected.PreparedRoots[0].PhysicalRootPath));
        Assert.False(Directory.Exists(expected.LogDirectory));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing")]
    [InlineData("unknown-member")]
    [InlineData("duplicate-member")]
    [InlineData("relative-source")]
    [InlineData("unbounded-timeout")]
    public async Task RuntimeDocument_InvalidOrMissing_FailsWithoutAmbientFallbackAsync(string fault)
    {
        using var file = new Document();
        var value = file.Valid();
        if (fault == "relative-source") value = value with { PreparedRoots = [value.PreparedRoots[0] with { PhysicalRootPath = "relative/source" }] };
        if (fault == "unbounded-timeout") value = value with { JavaNavigation = value.JavaNavigation with { RequestTimeout = TimeSpan.FromHours(1) } };
        string json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (fault == "unknown-member") json = "{\"ambientFallback\":true," + json[1..];
        if (fault == "duplicate-member") json = "{\"logDirectory\":\"C:/conflicting\"," + json[1..];
        file.Write(json);
        var configuration = fault == "missing" ? new ConfigurationBuilder().Build() : file.Configuration();
        await Assert.ThrowsAnyAsync<Exception>(() => OctoberGeneratorEntryPoint.ReadRuntimeConfigurationAsync(configuration));
        Assert.False(Directory.Exists(value.LogDirectory));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DriveRootSource_RejectsNestedStateOrLogWithoutFilesystemProbeAsync(bool overlapLog)
    {
        using var file = new Document();
        var value = file.Valid();
        string driveRoot = Path.GetPathRoot(value.PreparedRoots[0].PhysicalRootPath)!;
        value = value with { PreparedRoots = [value.PreparedRoots[0] with { PhysicalRootPath = driveRoot }] };
        if (overlapLog) value = value with { LogDirectory = Path.Combine(driveRoot, "owned-state-never-created") };
        else value = value with { JavaNavigation = value.JavaNavigation with { DisposableStateRoot = Path.Combine(driveRoot, "owned-state-never-created") } };
        file.Write(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await Assert.ThrowsAnyAsync<Exception>(() => OctoberGeneratorEntryPoint.ReadRuntimeConfigurationAsync(file.Configuration()));
    }
    private sealed class Document : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "october-runtime-config-test-" + Guid.NewGuid().ToString("N"));
        internal Document() => Directory.CreateDirectory(_directory);
        internal void Write(string json) => File.WriteAllText(Path.Combine(_directory, "runtime.json"), json);
        internal IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["October:RuntimeFile"] = Path.Combine(_directory, "runtime.json") }).Build();
        internal OctoberRuntimeConfiguration Valid() => new(
            [new("owned-target", Guid.Parse("06351469-5387-424a-8c04-d9f97d6b75e2"), Guid.Parse("595513fd-10fa-44b6-8c2e-bae0311470db"), "owned-root", Path.Combine(_directory, "unopened-source"))],
            Path.Combine(_directory, "unexecuted-tools", "codegraph.ps1"),
            new(Path.Combine(_directory, "uncreated-state"), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
            new([Path.Combine(_directory, "unexecuted-tools")], [Path.Combine(_directory, "runtime")], Path.Combine(_directory, "system", "cmd.exe"), [Path.Combine(_directory, "unexecuted-tools")], Path.Combine(_directory, "jdk", "bin")),
            Path.Combine(_directory, "uncreated-log"));
        public void Dispose() => Directory.Delete(_directory, true);
    }
}
