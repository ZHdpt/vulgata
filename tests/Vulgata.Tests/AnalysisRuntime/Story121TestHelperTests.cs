using Vulgata.Tests.TestInfrastructure;
using Vulgata.Tests.DurableScanning;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class Story121TestHelperTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void FixedClockAndIds_RepeatedInputs_AreDeterministicAndMonotonic()
    {
        DateTimeOffset initial = new(2026, 8, 30, 1, 2, 3, TimeSpan.Zero);
        FixedTestClock clock = new(initial);

        Guid first = DeterministicTestIds.Create("scan-job", 7, initial);
        Guid repeat = DeterministicTestIds.Create("scan-job", 7, initial);
        Guid different = DeterministicTestIds.Create("scan-job", 8, initial);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(first, repeat);
        Assert.NotEqual(first, different);
        Assert.Equal(initial.AddMinutes(2), clock.GetUtcNow());
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CanonicalHash_EquivalentObjectOrder_ProducesSameLowercaseSha256()
    {
        string first = Utf8CanonicalHash.ComputeJson("""{"z":2,"a":{"y":1,"x":0}}""");
        string reordered = Utf8CanonicalHash.ComputeJson("""{ "a": { "x": 0, "y": 1 }, "z": 2 }""");

        Assert.Equal(first, reordered);
        Assert.Equal(64, first.Length);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ProcessAndFileSystemDoubles_AlreadyCancelled_ObserveCancellationWithoutRecordingWork()
    {
        RecordingProcessLauncherDouble process = new();
        InMemoryFileSystemDouble fileSystem = new();
        fileSystem.AddUtf8(Path.Combine(Path.GetTempPath(), "story-12-1", "Fixture.java"), "class Fixture {}");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process.LaunchAsync(
            new TestProcessRequest("java", ["-version"], Path.GetTempPath(), new Dictionary<string, string?>()),
            cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fileSystem.ReadAllBytesAsync(
            Path.Combine(Path.GetTempPath(), "story-12-1", "Fixture.java"),
            cancellation.Token));

        Assert.True(process.ObservedCancellation);
        Assert.Empty(process.Requests);
        Assert.True(fileSystem.ObservedCancellation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Doubles_ConfiguredResultsAndFiles_AreDeterministicAndReturnDefensiveCopies()
    {
        string root = Path.Combine(Path.GetTempPath(), "story-12-1");
        string file = Path.Combine(root, "Fixture.java");
        InMemoryFileSystemDouble fileSystem = new();
        fileSystem.AddUtf8(file, "class Fixture {}");
        RecordingProcessLauncherDouble process = new();
        process.Enqueue(new TestProcessResult(0, "ready", string.Empty));

        byte[] firstRead = await fileSystem.ReadAllBytesAsync(file);
        firstRead[0] = 0;
        byte[] secondRead = await fileSystem.ReadAllBytesAsync(file);
        IReadOnlyList<string> files = await fileSystem.EnumerateFilesAsync(root);
        TestProcessResult result = await process.LaunchAsync(
            new TestProcessRequest("codegraph", ["status"], root, new Dictionary<string, string?>()));

        Assert.Equal((byte)'c', secondRead[0]);
        Assert.Equal([Path.GetFullPath(file)], files);
        Assert.Equal("ready", result.StandardOutput);
        Assert.Single(process.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void SanitizedDiagnostics_SecretsPathsAndLongInput_IsRedactedAndBounded()
    {
        string input = "token=very-secret C:\\Users\\operator\\repo\\source.cs /srv/vulgata/source.cs " + new string('x', 500);

        string diagnostic = BoundedSanitizedDiagnostics.Create(input, maximumLength: 96);

        Assert.DoesNotContain("very-secret", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("operator", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("/srv", diagnostic, StringComparison.Ordinal);
        Assert.Contains("[redacted]", diagnostic, StringComparison.Ordinal);
        Assert.Contains("[path]", diagnostic, StringComparison.Ordinal);
        Assert.Equal(96, diagnostic.Length);
        Assert.EndsWith("…", diagnostic, StringComparison.Ordinal);
    }
}
