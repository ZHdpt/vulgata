using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class DemoScanUiStateTests
{
    private static readonly DateTimeOffset _now =
        new(2026, 8, 15, 10, 0, 0, TimeSpan.FromHours(8));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void GetSnapshot_SameRepository_ReturnsStableReadyValues()
    {
        var state = new DemoScanUiState(new FixedTimeProvider(_now));
        var repositoryId = Guid.NewGuid();

        DemoRepositoryScanSnapshot first = state.GetSnapshot(repositoryId);
        DemoRepositoryScanSnapshot second = state.GetSnapshot(repositoryId);

        Assert.Equal(DemoRepositoryScanPhase.Ready, first.Phase);
        Assert.Equal(first, second);
        Assert.InRange(first.LastScannedAt, _now.AddDays(-7), _now.AddDays(-1));
        Assert.InRange(first.DocumentCount, 48, 240);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Start_ReadyRepository_ChangesOnlyThatRepositoryToScanning()
    {
        var state = new DemoScanUiState(new FixedTimeProvider(_now));
        var targetId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        DemoRepositoryScanSnapshot otherBefore = state.GetSnapshot(otherId);

        state.Start(targetId);

        Assert.Equal(DemoRepositoryScanPhase.Scanning, state.GetSnapshot(targetId).Phase);
        Assert.Equal(otherBefore, state.GetSnapshot(otherId));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Complete_ScanningRepository_ProducesSuccessfulFutureSummary()
    {
        var clock = new FixedTimeProvider(_now);
        var state = new DemoScanUiState(clock);
        var repositoryId = Guid.NewGuid();
        DemoRepositoryScanSnapshot initial = state.GetSnapshot(repositoryId);
        state.Start(repositoryId);

        clock.SetUtcNow(_now.AddMinutes(2));
        state.Complete(repositoryId);

        DemoRepositoryScanSnapshot completed = state.GetSnapshot(repositoryId);
        Assert.Equal(DemoRepositoryScanPhase.Completed, completed.Phase);
        Assert.InRange(completed.LastScannedAt, _now.AddMinutes(47), _now.AddMinutes(77));
        Assert.True(completed.DocumentCount > initial.DocumentCount);
    }

    private sealed class FixedTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        private static readonly TimeZoneInfo _chinaTimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Shanghai",
            TimeSpan.FromHours(8),
            "Asia/Shanghai",
            "Asia/Shanghai");
        private DateTimeOffset _utcNow = localNow.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => _chinaTimeZone;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value.ToUniversalTime();
    }
}
