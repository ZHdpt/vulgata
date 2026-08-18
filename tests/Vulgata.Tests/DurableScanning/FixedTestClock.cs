namespace Vulgata.Tests.DurableScanning;

internal sealed class FixedTestClock(DateTimeOffset initialUtcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = RequireUtc(initialUtcNow);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Test time cannot move backwards.");
        }

        _utcNow = _utcNow.Add(elapsed);
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        DateTimeOffset normalized = RequireUtc(utcNow);
        if (normalized < _utcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(utcNow), "Test time cannot move backwards.");
        }

        _utcNow = normalized;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("Fixed test time must use the UTC offset.", nameof(value));
}
