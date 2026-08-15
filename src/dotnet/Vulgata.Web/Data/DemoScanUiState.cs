namespace Vulgata.Web.Data;

public enum DemoRepositoryScanPhase
{
    Ready,
    Scanning,
    Completed,
}

public sealed record DemoRepositoryScanSnapshot(
    DemoRepositoryScanPhase Phase,
    DateTimeOffset LastScannedAt,
    int DocumentCount);

/// <summary>
/// Holds screen-recording-only repository scan values for the lifetime of one Blazor circuit.
/// </summary>
public sealed class DemoScanUiState(TimeProvider timeProvider)
{
    private readonly Dictionary<Guid, DemoRepositoryScanSnapshot> _snapshots = [];
    private readonly Random _random = new();

    public DemoRepositoryScanSnapshot GetSnapshot(Guid repositoryId)
    {
        if (_snapshots.TryGetValue(repositoryId, out var snapshot))
        {
            return snapshot;
        }

        DateTimeOffset now = timeProvider.GetLocalNow();
        snapshot = new DemoRepositoryScanSnapshot(
            DemoRepositoryScanPhase.Ready,
            now.AddMinutes(-_random.Next(24 * 60, (7 * 24 * 60) + 1)),
            _random.Next(48, 241));
        _snapshots.Add(repositoryId, snapshot);
        return snapshot;
    }

    public void Start(Guid repositoryId)
    {
        DemoRepositoryScanSnapshot current = GetSnapshot(repositoryId);
        _snapshots[repositoryId] = current with
        {
            Phase = DemoRepositoryScanPhase.Scanning,
        };
    }

    public void Complete(Guid repositoryId)
    {
        DemoRepositoryScanSnapshot current = GetSnapshot(repositoryId);
        _snapshots[repositoryId] = current with
        {
            Phase = DemoRepositoryScanPhase.Completed,
            LastScannedAt = timeProvider.GetLocalNow().AddMinutes(_random.Next(45, 76)),
            DocumentCount = current.DocumentCount + _random.Next(40, 161),
        };
    }
}
