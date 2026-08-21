using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Defines the fixed, source-free timing policy for the Worker Agent provider
/// recovery probe. Provider requests must not add Repository evidence.
/// </summary>
public static class WorkerAgentProviderHealthProbe
{
    private static readonly TimeSpan[] _probeDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
    ];

    private static readonly TimeSpan _maximumRetryAfter = TimeSpan.FromMinutes(10);

    public static TimeSpan GetProbeDelay(int probeOrdinal)
    {
        if (probeOrdinal is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(probeOrdinal));
        }

        return _probeDelays[probeOrdinal - 1];
    }

    public static TimeSpan ComputeProbeJitter(Guid pairExecutionId, int probeOrdinal)
    {
        if (pairExecutionId == Guid.Empty)
        {
            throw new ArgumentException("A pair execution identity is required.", nameof(pairExecutionId));
        }

        TimeSpan nominalDelay = GetProbeDelay(probeOrdinal);
        Span<byte> input = stackalloc byte[20];
        pairExecutionId.TryWriteBytes(input[..16], bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(input[16..], probeOrdinal);
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, digest);

        long exclusiveUpperBoundTicks = nominalDelay.Ticks / 10;
        ulong sample = BinaryPrimitives.ReadUInt64BigEndian(digest);
        long jitterTicks = (long)(sample % (ulong)exclusiveUpperBoundTicks);
        return TimeSpan.FromTicks(jitterTicks);
    }

    public static DateTimeOffset GetProbeEligibility(
        Guid pairExecutionId,
        int probeOrdinal,
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt)
    {
        RequireUtc(observedAt, nameof(observedAt));
        if (retryAfterAt is not null)
        {
            RequireUtc(retryAfterAt.Value, nameof(retryAfterAt));
        }

        DateTimeOffset nominalEligibility = observedAt
            .Add(GetProbeDelay(probeOrdinal))
            .Add(ComputeProbeJitter(pairExecutionId, probeOrdinal));
        if (retryAfterAt is null || retryAfterAt <= nominalEligibility)
        {
            return nominalEligibility;
        }

        DateTimeOffset maximumEligibility = observedAt.Add(_maximumRetryAfter);
        return retryAfterAt < maximumEligibility ? retryAfterAt.Value : maximumEligibility;
    }

    public static DateTimeOffset? BoundRetryAfter(
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt)
    {
        RequireUtc(observedAt, nameof(observedAt));
        if (retryAfterAt is null)
        {
            return null;
        }

        RequireUtc(retryAfterAt.Value, nameof(retryAfterAt));
        if (retryAfterAt <= observedAt)
        {
            return null;
        }

        DateTimeOffset maximum = observedAt.Add(_maximumRetryAfter);
        return retryAfterAt < maximum ? retryAfterAt : maximum;
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A UTC timestamp is required.", parameterName);
        }
    }
}
