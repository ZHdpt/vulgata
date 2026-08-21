namespace Vulgata.Core.Entities;

public sealed class Pass1ConcurrencyChange
{
    private Pass1ConcurrencyChange()
    {
    }

    public Guid Id { get; private set; }
    public Guid PairExecutionId { get; private set; }
    public long ExpectedStateVersion { get; private set; }
    public long ResultingStateVersion { get; private set; }
    public int PreviousLimit { get; private set; }
    public int EffectiveLimit { get; private set; }
    public long ConcurrencyRevision { get; private set; }
    public string ChangedByPrincipalId { get; private set; } = string.Empty;
    public string ChangeFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; private set; }
}
