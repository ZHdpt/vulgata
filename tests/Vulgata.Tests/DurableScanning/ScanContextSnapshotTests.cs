using Vulgata.Core.Entities;

namespace Vulgata.Tests.DurableScanning;

public sealed class ScanContextSnapshotTests
{
    private static readonly Guid _repositoryId =
        Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");

    private static readonly DateTimeOffset _scanStartedAt =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    private const string _profiledRevision =
        "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Capture_ValidContext_ProducesCanonicalImmutableBindingAndRejectsCaptureBeforeScanRunStart()
    {
        ScanRun scanRun = ScanRun.Start(_repositoryId, 1_800, _scanStartedAt);
        scanRun.BindProfiledRevision(_profiledRevision);
        DateTimeOffset capturedAt = _scanStartedAt.AddMinutes(2);

        ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
            scanRun,
            globalContext: "  global guidance  ",
            systemContext: "system guidance\n",
            repositoryContext: "\trepository guidance",
            capturedAt: capturedAt);
        ScanContextSnapshot repeat = ScanContextSnapshot.Capture(
            scanRun,
            globalContext: "global guidance",
            systemContext: "system guidance",
            repositoryContext: "repository guidance",
            capturedAt: capturedAt);

        Assert.Equal(snapshot.Id, repeat.Id);
        Assert.Equal(snapshot.ContentHash, repeat.ContentHash);
        Assert.Equal("scan-context-snapshot/1.0", snapshot.SchemaIdentity);
        Assert.Equal(_repositoryId, snapshot.RepositoryId);
        Assert.Equal(scanRun.Id, snapshot.ScanRunId);
        Assert.Equal(_profiledRevision, snapshot.ProfiledRevision);
        Assert.Equal("global guidance", snapshot.GlobalContext);
        Assert.Equal("system guidance", snapshot.SystemContext);
        Assert.Equal("repository guidance", snapshot.RepositoryContext);
        Assert.Equal(capturedAt, snapshot.CapturedAt);
        Assert.Matches("^[0-9a-f]{64}$", snapshot.ContentHash);

        Assert.Throws<ArgumentOutOfRangeException>(() => ScanContextSnapshot.Capture(
            scanRun,
            globalContext: "global guidance",
            systemContext: "system guidance",
            repositoryContext: "repository guidance",
            capturedAt: _scanStartedAt.AddTicks(-1)));
    }
}
