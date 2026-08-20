using System.Security.Cryptography;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.CommunicationDetection;

public static class PreparedXmlRangeProvider
{
    public static PreparedSourceSplitPlan PlanRanges(
        ReadOnlyMemory<byte> source,
        SafeXmlDirectChildRangeSet rangeSet)
    {
        ArgumentNullException.ThrowIfNull(rangeSet);
        if (rangeSet.Outcome != SafeXmlOutcome.Parsed)
        {
            return PreparedSourceRangePlanner.RejectJavaRanges(
                rangeSet.SkipReason ?? "SafeXmlNotParsed");
        }

        string actualFileHash = Convert.ToHexStringLower(SHA256.HashData(source.Span));
        if (!StringComparer.Ordinal.Equals(actualFileHash, rangeSet.FileHash))
        {
            return PreparedSourceRangePlanner.RejectJavaRanges("FileHashMismatch");
        }

        SafeXmlDirectChildRange[] ranges = [.. rangeSet.Ranges];
        if (ranges.Length == 0)
        {
            return PreparedSourceRangePlanner.RejectJavaRanges("MissingExactDirectChildRange");
        }

        var planned = new List<PreparedJavaRange>(ranges.Length);
        long priorEnd = -1;
        for (int position = 0; position < ranges.Length; position++)
        {
            SafeXmlDirectChildRange range = ranges[position];
            if (range.ByteStart < 0 || range.ByteEndExclusive <= range.ByteStart ||
                range.ByteEndExclusive > source.Length || range.ByteStart < priorEnd ||
                range.LineStart < 1 || range.LineEnd < range.LineStart ||
                range.ByteEndExclusive - range.ByteStart > PreparedSourceRangePlanner.MaximumRawBytes ||
                range.LineEnd - range.LineStart + 1 > PreparedSourceRangePlanner.MaximumLogicalLines ||
                range.SegmentOrdinal != position + 1 || range.SegmentCount != ranges.Length ||
                !StringComparer.Ordinal.Equals(range.FileHash, actualFileHash))
            {
                return PreparedSourceRangePlanner.RejectJavaRanges("UnsafeOrOversizedDirectChildRange");
            }

            string sourceHash = PreparedSourceRangePlanner.HashRawInterval(
                source.Span,
                checked((int)range.ByteStart),
                checked((int)range.ByteEndExclusive));
            if (!StringComparer.Ordinal.Equals(sourceHash, range.SourceHash))
            {
                return PreparedSourceRangePlanner.RejectJavaRanges("SourceHashMismatch");
            }

            planned.Add(new(
                range.ByteStart,
                range.ByteEndExclusive,
                range.LineStart,
                range.LineEnd,
                actualFileHash,
                sourceHash,
                position + 1,
                ranges.Length,
                PreparedRangeOutcome.Exact,
                null));
            priorEnd = range.ByteEndExclusive;
        }

        return new PreparedSourceSplitPlan(PreparedRangeOutcome.Exact, null, planned);
    }
}
