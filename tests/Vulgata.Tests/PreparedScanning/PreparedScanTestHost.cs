namespace Vulgata.Tests.PreparedScanning;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PreparedScanningCollection : ICollectionFixture<PreparedScanTestHost>
{
    public const string Name = "Story 12.2 prepared scanning";
}

public sealed class PreparedScanTestHost : IDisposable
{
    public static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    public static readonly Guid CoordinationId =
        Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    public static readonly Guid FirstRepositoryId =
        Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
    public static readonly Guid SecondRepositoryId =
        Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
    public static readonly Guid FirstScanRunId =
        Guid.Parse("11111111-2222-4333-8444-555555555555");
    public static readonly Guid SecondScanRunId =
        Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");

    public PreparedScanTestHost()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story-12-2-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        Clock = new TestDoubles.FixedTimeProvider(FixedUtcNow);
    }

    public string RootPath { get; }
    public TimeProvider Clock { get; }

    public string CreateContainedDirectory(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("A single safe directory name is required.", nameof(name));
        }

        string path = Path.Combine(RootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
