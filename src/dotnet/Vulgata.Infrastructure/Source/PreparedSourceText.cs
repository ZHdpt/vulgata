using System.Buffers;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

internal static class PreparedSourceText
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal sealed record DecodeResult(PreparedSourceOutcome Outcome, string? Text);

    internal sealed record LiteralMatch(long ByteStart, int Line, int UnicodeScalarColumn);

    internal static byte[]? EncodeLiteral(string? literal)
    {
        if (string.IsNullOrEmpty(literal) || literal.Contains('\r') || literal.Contains('\n'))
        {
            return null;
        }

        try
        {
            byte[] bytes = StrictUtf8.GetBytes(literal);
            return bytes.Length is > 0 and <= PreparedSourceCallLimits.MaximumSearchLiteralUtf8Bytes
                ? bytes
                : null;
        }
        catch (EncoderFallbackException)
        {
            return null;
        }
    }

    internal sealed class LiteralScanner
    {
        private readonly byte[] _query;
        private readonly int[] _prefix;
        private readonly (int Line, int Column)[] _locations;
        private readonly byte[] _pendingScalar = new byte[4];
        private int _pendingCount;
        private int _expectedScalarLength;
        private int _matched;
        private long _processedBytes;
        private int _line = 1;
        private int _column = 1;
        private bool _previousWasCarriageReturn;

        internal LiteralScanner(byte[] query)
        {
            _query = query;
            _prefix = BuildPrefix(query);
            _locations = new (int, int)[query.Length];
        }

        internal bool UnsupportedText { get; private set; }

        internal bool HasIncompleteScalar => _pendingCount != 0;

        internal bool Process(ReadOnlySpan<byte> chunk, Func<LiteralMatch, bool> onMatch)
        {
            foreach (byte value in chunk)
            {
                if (_pendingCount == 0)
                {
                    _expectedScalarLength = value < 0x80 ? 1 : ScalarLength(value);
                    if (_expectedScalarLength == 0)
                    {
                        UnsupportedText = true;
                        return false;
                    }
                }

                _pendingScalar[_pendingCount++] = value;
                if (_pendingCount < _expectedScalarLength)
                {
                    continue;
                }

                if (Rune.DecodeFromUtf8(
                        _pendingScalar.AsSpan(0, _pendingCount),
                        out Rune scalar,
                        out int consumed) != OperationStatus.Done ||
                    consumed != _pendingCount || scalar.Value == 0 ||
                    (scalar.Value == 0xFEFF && _processedBytes != 0))
                {
                    UnsupportedText = true;
                    return false;
                }

                for (int index = 0; index < _pendingCount; index++)
                {
                    _locations[(int)(_processedBytes % _query.Length)] = (_line, _column);
                    byte current = _pendingScalar[index];
                    while (_matched > 0 && _query[_matched] != current)
                    {
                        _matched = _prefix[_matched - 1];
                    }

                    if (_query[_matched] == current)
                    {
                        _matched++;
                    }

                    _processedBytes++;
                    if (_matched == _query.Length)
                    {
                        long start = _processedBytes - _query.Length;
                        (int line, int column) = _locations[(int)(start % _query.Length)];
                        _matched = _prefix[_matched - 1];
                        if (!onMatch(new LiteralMatch(start, line, column)))
                        {
                            _pendingCount = 0;
                            return false;
                        }
                    }
                }

                AdvancePosition(scalar);
                _pendingCount = 0;
            }

            return true;
        }

        private void AdvancePosition(Rune scalar)
        {
            if (scalar.Value == '\r')
            {
                _line++;
                _column = 1;
                _previousWasCarriageReturn = true;
            }
            else if (scalar.Value == '\n')
            {
                if (!_previousWasCarriageReturn)
                {
                    _line++;
                }

                _column = 1;
                _previousWasCarriageReturn = false;
            }
            else if (scalar.Value == 0xFEFF && _processedBytes == 3)
            {
                _previousWasCarriageReturn = false;
            }
            else
            {
                _column++;
                _previousWasCarriageReturn = false;
            }
        }

        private static int[] BuildPrefix(byte[] query)
        {
            int[] prefix = new int[query.Length];
            for (int index = 1, matched = 0; index < query.Length; index++)
            {
                while (matched > 0 && query[index] != query[matched])
                {
                    matched = prefix[matched - 1];
                }

                if (query[index] == query[matched])
                {
                    matched++;
                }

                prefix[index] = matched;
            }

            return prefix;
        }
    }

    internal static DecodeResult DecodeRange(
        SafeFileHandle file,
        ReadOnlySpan<byte> bytes,
        long byteOffset,
        long fileLength,
        Func<int, bool> reserveExaminedBytes)
    {
        try
        {
            string text = StrictUtf8.GetString(bytes);
            if (text.Contains('\0') ||
                (text.Contains('\uFEFF') &&
                 (byteOffset != 0 || text.IndexOf('\uFEFF', 1) >= 0)))
            {
                return new DecodeResult(PreparedSourceOutcome.UnsupportedText, null);
            }

            return new DecodeResult(PreparedSourceOutcome.Complete, text);
        }
        catch (DecoderFallbackException)
        {
            try
            {
                bool startsInside = StartsInsideScalar(
                    file,
                    bytes,
                    byteOffset,
                    fileLength,
                    reserveExaminedBytes,
                    out bool startProbeLimited);
                if (startProbeLimited)
                {
                    return new DecodeResult(PreparedSourceOutcome.Limited, null);
                }

                if (startsInside)
                {
                    return new DecodeResult(PreparedSourceOutcome.InvalidRange, null);
                }

                bool endsInside = EndsInsideScalar(
                    file,
                    bytes,
                    byteOffset,
                    fileLength,
                    reserveExaminedBytes,
                    out bool endProbeLimited);
                if (endProbeLimited)
                {
                    return new DecodeResult(PreparedSourceOutcome.Limited, null);
                }

                return new DecodeResult(
                    endsInside
                        ? PreparedSourceOutcome.InvalidRange
                        : PreparedSourceOutcome.UnsupportedText,
                    null);
            }
            catch (IOException)
            {
                return new DecodeResult(PreparedSourceOutcome.Unreadable, null);
            }
        }
    }

    private static bool StartsInsideScalar(
        SafeFileHandle file,
        ReadOnlySpan<byte> bytes,
        long byteOffset,
        long fileLength,
        Func<int, bool> reserveExaminedBytes,
        out bool probeLimited)
    {
        probeLimited = false;
        if (byteOffset == 0 || bytes.IsEmpty || !IsContinuation(bytes[0]))
        {
            return false;
        }

        int precedingCount = (int)Math.Min(3, byteOffset);
        if (!reserveExaminedBytes(precedingCount))
        {
            probeLimited = true;
            return false;
        }

        Span<byte> preceding = stackalloc byte[3];
        int read = RandomAccess.Read(
            file,
            preceding[..precedingCount],
            byteOffset - precedingCount);
        for (int index = read - 1; index >= 0; index--)
        {
            int scalarLength = ScalarLength(preceding[index]);
            if (scalarLength > 0)
            {
                long leadOffset = byteOffset - read + index;
                return IsValidScalarCrossing(
                    file,
                    leadOffset,
                    byteOffset,
                    fileLength,
                    scalarLength,
                    reserveExaminedBytes,
                    out probeLimited);
            }

            if (!IsContinuation(preceding[index]))
            {
                return false;
            }
        }

        return false;
    }

    private static bool EndsInsideScalar(
        SafeFileHandle file,
        ReadOnlySpan<byte> bytes,
        long byteOffset,
        long fileLength,
        Func<int, bool> reserveExaminedBytes,
        out bool probeLimited)
    {
        probeLimited = false;
        long end = byteOffset + bytes.Length;
        if (bytes.IsEmpty || end >= fileLength)
        {
            return false;
        }

        int first = Math.Max(0, bytes.Length - 3);
        for (int index = bytes.Length - 1; index >= first; index--)
        {
            int scalarLength = ScalarLength(bytes[index]);
            if (scalarLength > 0)
            {
                try
                {
                    StrictUtf8.GetString(bytes[..index]);
                }
                catch (DecoderFallbackException)
                {
                    return false;
                }

                return IsValidScalarCrossing(
                    file,
                    byteOffset + index,
                    end,
                    fileLength,
                    scalarLength,
                    reserveExaminedBytes,
                    out probeLimited);
            }

            if (!IsContinuation(bytes[index]))
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsValidScalarCrossing(
        SafeFileHandle file,
        long leadOffset,
        long boundary,
        long fileLength,
        int scalarLength,
        Func<int, bool> reserveExaminedBytes,
        out bool probeLimited)
    {
        probeLimited = false;
        if (boundary <= leadOffset ||
            boundary >= leadOffset + scalarLength ||
            leadOffset + scalarLength > fileLength)
        {
            return false;
        }

        if (!reserveExaminedBytes(scalarLength))
        {
            probeLimited = true;
            return false;
        }

        Span<byte> scalarBytes = stackalloc byte[4];
        int read = RandomAccess.Read(file, scalarBytes[..scalarLength], leadOffset);
        return read == scalarLength &&
            Rune.DecodeFromUtf8(
                scalarBytes[..scalarLength],
                out _,
                out int consumed) == OperationStatus.Done &&
            consumed == scalarLength;
    }

    private static bool IsContinuation(byte value) => (value & 0xC0) == 0x80;

    private static int ScalarLength(byte lead) => lead switch
    {
        >= 0xC2 and <= 0xDF => 2,
        >= 0xE0 and <= 0xEF => 3,
        >= 0xF0 and <= 0xF4 => 4,
        _ => 0,
    };
}
