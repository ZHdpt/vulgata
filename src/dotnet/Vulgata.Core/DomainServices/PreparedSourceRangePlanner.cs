using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.DomainServices;

public sealed record PreparedSourceRange(
    long ByteStart,
    long ByteEndExclusive,
    int LineStart,
    int LineEnd,
    string SourceHash);

public enum PreparedRangeOutcome
{
    Exact,
    Skipped,
}

/// <summary>Zero-based, end-exclusive UTF-16 positions returned by JDT LS.</summary>
public sealed record PreparedJdtSymbolSpan(
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);

public sealed record PreparedJavaRange(
    long ByteStart,
    long ByteEndExclusive,
    int LineStart,
    int LineEnd,
    string FileHash,
    string SourceHash,
    int SegmentOrdinal,
    int SegmentCount,
    PreparedRangeOutcome Outcome,
    string? SkipReason);

public sealed record PreparedSourceSplitPlan(
    PreparedRangeOutcome Outcome,
    string? SkipReason,
    IReadOnlyList<PreparedJavaRange> Ranges);

public static class PreparedSourceRangePlanner
{
    public const int MaximumRawBytes = 65_536;
    public const int MaximumLogicalLines = 2_000;

    public static int CountLogicalLines(ReadOnlySpan<byte> source)
    {
        int lines = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == (byte)'\r')
            {
                lines++;
                if (index + 1 < source.Length && source[index + 1] == (byte)'\n')
                {
                    index++;
                }
            }
            else if (source[index] == (byte)'\n')
            {
                lines++;
            }
        }

        if (!source.IsEmpty && source[^1] is not ((byte)'\r') and not ((byte)'\n'))
        {
            lines++;
        }

        return lines;
    }

    public static string HashRawInterval(
        ReadOnlySpan<byte> source,
        int byteStart,
        int byteEndExclusive)
    {
        if (byteStart < 0 || byteEndExclusive <= byteStart || byteEndExclusive > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteStart));
        }

        return Convert.ToHexStringLower(SHA256.HashData(source[byteStart..byteEndExclusive]));
    }

    public static PreparedSourceRange Create(
        ReadOnlySpan<byte> source,
        int byteStart,
        int byteEndExclusive,
        int lineStart)
    {
        if (lineStart < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineStart));
        }

        int byteCount = byteEndExclusive - byteStart;
        if (byteCount <= 0 || byteCount > MaximumRawBytes || byteStart < 0 || byteEndExclusive > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteEndExclusive));
        }

        int lineCount = CountLogicalLines(source[byteStart..byteEndExclusive]);
        if (lineCount is < 1 or > MaximumLogicalLines)
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        return new PreparedSourceRange(
            byteStart,
            byteEndExclusive,
            lineStart,
            checked(lineStart + lineCount - 1),
            HashRawInterval(source, byteStart, byteEndExclusive));
    }

    public static PreparedSourceSplitPlan PlanJavaRanges(
        ReadOnlyMemory<byte> source,
        string expectedFileHash,
        IEnumerable<PreparedJdtSymbolSpan> topLevelRanges)
    {
        ArgumentNullException.ThrowIfNull(topLevelRanges);
        string actualFileHash = Convert.ToHexStringLower(SHA256.HashData(source.Span));
        if (!StringComparer.Ordinal.Equals(actualFileHash, expectedFileHash))
        {
            return RejectJavaRanges("FileHashMismatch");
        }

        PreparedJdtSymbolSpan[] spans = topLevelRanges.ToArray();
        if (spans.Length == 0 || spans.Any(span => span is null))
        {
            return RejectJavaRanges("MissingExactTopLevelRange");
        }

        try
        {
            var index = new Utf8PositionIndex(source.Span);
            var mapped = new List<(int ByteStart, int ByteEnd, int LineStart, int LineEnd)>(spans.Length);
            foreach (PreparedJdtSymbolSpan span in spans)
            {
                int byteStart = index.GetByteOffset(span.StartLine, span.StartCharacter);
                int byteEnd = index.GetByteOffset(span.EndLine, span.EndCharacter);
                if (byteEnd <= byteStart)
                {
                    return RejectJavaRanges("MalformedTopLevelRange");
                }

                int lineStart = checked(span.StartLine + 1);
                int lineEnd = span.EndCharacter == 0
                    ? span.EndLine
                    : checked(span.EndLine + 1);
                if (lineEnd < lineStart)
                {
                    return RejectJavaRanges("MalformedTopLevelRange");
                }

                mapped.Add((byteStart, byteEnd, lineStart, lineEnd));
            }

            mapped.Sort(static (left, right) => left.ByteStart.CompareTo(right.ByteStart));
            for (int position = 0; position < mapped.Count; position++)
            {
                (int byteStart, int byteEnd, int lineStart, int lineEnd) = mapped[position];
                if ((position > 0 && mapped[position - 1].ByteEnd > byteStart) ||
                    byteEnd - byteStart > MaximumRawBytes ||
                    lineEnd - lineStart + 1 > MaximumLogicalLines)
                {
                    return RejectJavaRanges("UnsafeOrOversizedTopLevelRange");
                }
            }

            PreparedJavaRange[] ranges = mapped
                .Select((range, position) => new PreparedJavaRange(
                    range.ByteStart,
                    range.ByteEnd,
                    range.LineStart,
                    range.LineEnd,
                    actualFileHash,
                    HashRawInterval(source.Span, range.ByteStart, range.ByteEnd),
                    position + 1,
                    mapped.Count,
                    PreparedRangeOutcome.Exact,
                    null))
                .ToArray();
            return new PreparedSourceSplitPlan(PreparedRangeOutcome.Exact, null, ranges);
        }
        catch (DecoderFallbackException)
        {
            return RejectJavaRanges("InvalidUtf8");
        }
        catch (ArgumentException)
        {
            return RejectJavaRanges("MalformedTopLevelRange");
        }
        catch (OverflowException)
        {
            return RejectJavaRanges("RangeOverflow");
        }
    }

    public static PreparedSourceSplitPlan RejectJavaRanges(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new PreparedSourceSplitPlan(PreparedRangeOutcome.Skipped, reason, []);
    }

    private sealed class Utf8PositionIndex
    {
        private static readonly UTF8Encoding _strictUtf8 = new(false, true);
        private readonly byte[] _source;
        private readonly int[] _lineStarts;

        public Utf8PositionIndex(ReadOnlySpan<byte> source)
        {
            _source = source.ToArray();
            _ = _strictUtf8.GetString(_source);
            var starts = new List<int> { HasUtf8Bom(_source) ? 3 : 0 };
            for (int index = starts[0]; index < _source.Length; index++)
            {
                if (_source[index] == (byte)'\r')
                {
                    if (index + 1 < _source.Length && _source[index + 1] == (byte)'\n')
                    {
                        index++;
                    }

                    starts.Add(index + 1);
                }
                else if (_source[index] == (byte)'\n')
                {
                    starts.Add(index + 1);
                }
            }

            _lineStarts = starts.ToArray();
        }

        public int GetByteOffset(int line, int utf16Character)
        {
            if (line < 0 || line >= _lineStarts.Length || utf16Character < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(line));
            }

            int start = _lineStarts[line];
            int end = line + 1 < _lineStarts.Length
                ? ExcludeLineTerminator(_source, _lineStarts[line + 1])
                : _source.Length;
            string lineText = _strictUtf8.GetString(_source.AsSpan(start, end - start));
            if (utf16Character > lineText.Length ||
                (utf16Character > 0 && utf16Character < lineText.Length &&
                    char.IsHighSurrogate(lineText[utf16Character - 1]) &&
                    char.IsLowSurrogate(lineText[utf16Character])))
            {
                throw new ArgumentOutOfRangeException(nameof(utf16Character));
            }

            return checked(start + _strictUtf8.GetByteCount(lineText.AsSpan(0, utf16Character)));
        }

        private static int ExcludeLineTerminator(byte[] source, int nextLineStart)
        {
            int end = nextLineStart;
            if (end > 0 && source[end - 1] == (byte)'\n')
            {
                end--;
            }

            if (end > 0 && source[end - 1] == (byte)'\r')
            {
                end--;
            }

            return end;
        }

        private static bool HasUtf8Bom(ReadOnlySpan<byte> source) =>
            source.Length >= 3 && source[0] == 0xef && source[1] == 0xbb && source[2] == 0xbf;
    }
}
