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

    public static Pass1ConcurrencyChange Create(
        Guid id,
        Guid pairExecutionId,
        long expectedStateVersion,
        long resultingStateVersion,
        int previousLimit,
        int effectiveLimit,
        long concurrencyRevision,
        string changedByPrincipalId,
        string changeFingerprint,
        DateTimeOffset changedAt)
    {
        if (id == Guid.Empty || pairExecutionId == Guid.Empty)
        {
            throw new ArgumentException("Complete concurrency-change identities are required.");
        }

        if (expectedStateVersion < 0 || resultingStateVersion != expectedStateVersion + 1 ||
            concurrencyRevision <= 0 || previousLimit is < 1 or > 4 ||
            effectiveLimit is < 1 or > 4 || previousLimit == effectiveLimit)
        {
            throw new ArgumentException("The concurrency transition is invalid.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(changedByPrincipalId);
        if (changedByPrincipalId.Length > 256 || changeFingerprint.Length != 64 ||
            changeFingerprint.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character)) ||
            changedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The concurrency audit binding is invalid.");
        }

        return new Pass1ConcurrencyChange
        {
            Id = id,
            PairExecutionId = pairExecutionId,
            ExpectedStateVersion = expectedStateVersion,
            ResultingStateVersion = resultingStateVersion,
            PreviousLimit = previousLimit,
            EffectiveLimit = effectiveLimit,
            ConcurrencyRevision = concurrencyRevision,
            ChangedByPrincipalId = changedByPrincipalId,
            ChangeFingerprint = changeFingerprint,
            ChangedAt = changedAt,
        };
    }
}
