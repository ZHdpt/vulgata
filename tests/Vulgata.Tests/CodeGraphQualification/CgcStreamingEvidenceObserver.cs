using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Tests.CodeGraphQualification;

/// <summary>
/// Qualification-only streaming observer. It deliberately exposes only bounded,
/// redacted facts: byte counts, per-stream hashes, markers, and structural
/// predicates. Provider output is decoded and discarded character by character.
/// </summary>
internal sealed class CgcStreamingEvidenceObserver : IDisposable
{
    internal const int MaximumObservedBytes = 65_536;
    internal const int OverflowSentinelBytes = MaximumObservedBytes + 1;
    internal const int MaximumStructuralRecordBytes = 4_096;
    private const int _maximumQualifiedCommandEvidenceCount = 11;

    private const string _beginMarker = "VULGATA_CGC_QUERY_OUTPUT=BEGIN";
    private const string _endMarker = "VULGATA_CGC_QUERY_OUTPUT=END";
    private const string _nonRootMarker = "VULGATA_CGC_QUALIFICATION_NON_ROOT=SUCCEEDED";
    private const string _indexMarker = "VULGATA_CGC_QUALIFICATION_INDEX=SUCCEEDED";
    private const string _cleanupAttemptedMarker = "VULGATA_CGC_QUALIFICATION_CLEANUP=ATTEMPTED";
    private const string _cleanupMarker = "VULGATA_CGC_QUALIFICATION_CLEANUP=SUCCEEDED";
    private readonly object _gate = new();
    private readonly Action? _overflowSignal;
    private readonly Decoder _standardOutputDecoder = new UTF8Encoding(false, true).GetDecoder();
    private readonly Decoder _standardErrorDecoder = new UTF8Encoding(false, true).GetDecoder();
    private readonly IncrementalHash _standardOutputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly IncrementalHash _standardErrorHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly StreamingLineMatcher _standardOutputLineMatcher = new(_beginMarker, _endMarker);
    private readonly ExactSafeLineMatcher _lifecycleLineMatcher = new(_nonRootMarker, _indexMarker, _cleanupAttemptedMarker, _cleanupMarker);
    private readonly CgcCommandEvidenceRecognizer _commandEvidenceRecognizer = new();
    private readonly List<QualificationCommandEvidence> _commandEvidence = [];
    private readonly CgcStructuralRecordRecognizer _structuralRecordRecognizer = new();
    private long _standardOutputBytes;
    private long _standardErrorBytes;
    private int _standardOutputLineBytes;
    private int _stdoutBeginCount;
    private int _stdoutEndCount;
    private bool _markerOrderInvalid;
    private bool _ranAsNonRoot;
    private bool _indexedWholeRepository;
    private bool _cleanupAttempted;
    private bool _cleanupSucceeded;
    private bool _commandEvidenceOverflowed;
    private bool _invalidUtf8;
    private bool _overflowed;
    private bool _completed;
    private CgcQueryRegion _queryRegion;
    private CgcStreamingEvidence? _evidence;

    internal CgcStreamingEvidenceObserver(Action? overflowSignal = null)
    {
        _overflowSignal = overflowSignal;
    }

    internal CgcObservationOutcome Observe(CgcOutputStream stream, ReadOnlySpan<byte> bytes)
    {
        if (stream is not (CgcOutputStream.StandardOutput or CgcOutputStream.StandardError))
        {
            throw new ArgumentOutOfRangeException(nameof(stream));
        }

        lock (_gate)
        {
            ThrowIfCompleted();
            if (_overflowed)
            {
                return CgcObservationOutcome.OverflowAlreadyObserved;
            }

            foreach (byte value in bytes)
            {
                long observedBefore = _standardOutputBytes + _standardErrorBytes;
                if (observedBefore == OverflowSentinelBytes)
                {
                    return CgcObservationOutcome.OverflowAlreadyObserved;
                }

                AddByte(stream, value);
                if (observedBefore == MaximumObservedBytes)
                {
                    _overflowed = true;
                    _overflowSignal?.Invoke();
                    return CgcObservationOutcome.OverflowTriggered;
                }
            }

            return CgcObservationOutcome.Continue;
        }
    }

    internal CgcStreamingEvidence Complete()
    {
        lock (_gate)
        {
            if (_evidence is not null)
            {
                return _evidence;
            }

            FlushDecoder(CgcOutputStream.StandardOutput);
            FlushDecoder(CgcOutputStream.StandardError);
            if (_queryRegion == CgcQueryRegion.Inside)
            {
                _markerOrderInvalid = true;
            }

            _completed = true;
            IReadOnlyDictionary<string, int> relationshipKindCounts = _structuralRecordRecognizer.GetRelationshipKindCounts();
            bool markerPair = !_overflowed &&
                !_invalidUtf8 &&
                !_markerOrderInvalid &&
                _stdoutBeginCount == 1 &&
                _stdoutEndCount == 1 &&
                _queryRegion == CgcQueryRegion.Ended;
            bool hasValidRecordGrammar = _structuralRecordRecognizer.ValidRecordCount > 0 &&
                _structuralRecordRecognizer.InvalidOrOversizedRecordCount == 0;
            string stdoutSha256 = Convert.ToHexString(_standardOutputHash.GetHashAndReset()).ToLowerInvariant();
            string stderrSha256 = Convert.ToHexString(_standardErrorHash.GetHashAndReset()).ToLowerInvariant();
            bool containerCleanupForced = _overflowed;
            string canonicalSummarySha256 = CreateCanonicalSummarySha256(
                stdoutSha256,
                stderrSha256,
                relationshipKindCounts,
                markerPair,
                hasValidRecordGrammar,
                _cleanupAttempted,
                containerCleanupForced);
            _evidence = new CgcStreamingEvidence(
                IsValidUtf8: !_invalidUtf8,
                Overflowed: _overflowed,
                TerminationRequested: _overflowed,
                ObservedTotalBytes: _standardOutputBytes + _standardErrorBytes,
                StandardOutput: new CgcStreamEvidence(_standardOutputBytes, stdoutSha256),
                StandardError: new CgcStreamEvidence(_standardErrorBytes, stderrSha256),
                CanonicalSummarySha256: canonicalSummarySha256,
                StdoutBeginCount: _stdoutBeginCount,
                StdoutEndCount: _stdoutEndCount,
                HasExactlyOneOrderedStdoutMarkerPair: markerPair,
                RanAsNonRoot: _ranAsNonRoot,
                IndexedWholeRepository: _indexedWholeRepository,
                CleanupSucceeded: _cleanupSucceeded,
                CleanupAttempted: _cleanupAttempted,
                ContainerCleanupForced: containerCleanupForced,
                HasValidRecordGrammar: hasValidRecordGrammar,
                HasValidStructuralRecord: markerPair && hasValidRecordGrammar,
                ValidRecordCount: _structuralRecordRecognizer.ValidRecordCount,
                InvalidOrOversizedRecordCount: _structuralRecordRecognizer.InvalidOrOversizedRecordCount,
                RelationshipKindCounts: relationshipKindCounts,
                HasAllRequiredRelationshipKinds: relationshipKindCounts.Values.All(count => count > 0),
                HasFilteredTargetCandidate: _structuralRecordRecognizer.HasFilteredTargetCandidate,
                HasPositiveOrderedRange: _structuralRecordRecognizer.HasPositiveOrderedRange,
                HasNonEmptyEndpointIdentity: _structuralRecordRecognizer.HasNonEmptyEndpointIdentity,
                AllStructuralFactsCorrelatedWithinValidRecords: _structuralRecordRecognizer.AllFactsCorrelatedWithinValidRecords,
                Commands: _commandEvidence.ToArray(),
                CommandEvidenceOverflowed: _commandEvidenceOverflowed);
            return _evidence;
        }
    }

    private string CreateCanonicalSummarySha256(
        string stdoutSha256,
        string stderrSha256,
        IReadOnlyDictionary<string, int> relationshipKindCounts,
        bool queryCompleted,
        bool hasValidRecordGrammar,
        bool cleanupAttempted,
        bool containerCleanupForced)
    {
        string relationshipKinds = string.Join(
            ',',
            relationshipKindCounts.Where(pair => pair.Value > 0).Select(pair => pair.Key).OrderBy(kind => kind, StringComparer.Ordinal));
        string predicateCounts = string.Join(
            ',',
            $"valid={_structuralRecordRecognizer.ValidRecordCount}",
            $"invalidOrOversized={_structuralRecordRecognizer.InvalidOrOversizedRecordCount}",
            $"CALLS={relationshipKindCounts["CALLS"]}",
            $"CONTAINS={relationshipKindCounts["CONTAINS"]}",
            $"IMPORTS={relationshipKindCounts["IMPORTS"]}",
            $"INHERITS={relationshipKindCounts["INHERITS"]}",
            $"allRequiredRelationshipKindsObserved={ToLowerBoolean(relationshipKindCounts.Values.All(count => count > 0))}",
            $"allStructuralFactsCorrelatedWithinValidRecords={ToLowerBoolean(_structuralRecordRecognizer.AllFactsCorrelatedWithinValidRecords)}",
            $"hasFilteredTargetCandidate={ToLowerBoolean(_structuralRecordRecognizer.HasFilteredTargetCandidate)}",
            $"hasNonEmptyEndpointIdentity={ToLowerBoolean(_structuralRecordRecognizer.HasNonEmptyEndpointIdentity)}",
            $"hasPositiveOrderedRange={ToLowerBoolean(_structuralRecordRecognizer.HasPositiveOrderedRange)}",
            $"hasValidRecordGrammar={ToLowerBoolean(hasValidRecordGrammar)}",
            $"queryCompleted={ToLowerBoolean(queryCompleted)}");
        string summary = string.Join(
            '|',
            "1.1",
            _standardOutputBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _standardErrorBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            (_standardOutputBytes + _standardErrorBytes).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ToLowerBoolean(_overflowed),
            ToLowerBoolean(_overflowed),
            ToLowerBoolean(containerCleanupForced),
            stdoutSha256,
            stderrSha256,
            "vulgata-cgc-query-stdout-v1,vulgata-cgc-streaming-evidence-v1,vulgata-cgc-structural-record-v1",
            relationshipKinds,
            predicateCounts,
            ToLowerBoolean(cleanupAttempted),
            ToLowerBoolean(_cleanupSucceeded));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(summary))).ToLowerInvariant();
    }

    private static string ToLowerBoolean(bool value) => value ? "true" : "false";

    public void Dispose()
    {
        _standardOutputHash.Dispose();
        _standardErrorHash.Dispose();
    }

    private void AddByte(CgcOutputStream stream, byte value)
    {
        if (stream == CgcOutputStream.StandardOutput)
        {
            _standardOutputBytes++;
            _standardOutputHash.AppendData([value]);
            _standardOutputLineBytes++;
        }
        else
        {
            _standardErrorBytes++;
            _standardErrorHash.AppendData([value]);
        }

        DecodeByte(stream, value);
    }

    private void DecodeByte(CgcOutputStream stream, byte value)
    {
        if (_invalidUtf8)
        {
            return;
        }

        try
        {
            Decoder decoder = stream == CgcOutputStream.StandardOutput
                ? _standardOutputDecoder
                : _standardErrorDecoder;
            Span<byte> source = stackalloc byte[1];
            source[0] = value;
            Span<char> characters = stackalloc char[2];
            decoder.Convert(source, characters, flush: false, out int bytesUsed, out int charsUsed, out _);
            if (bytesUsed != source.Length)
            {
                _invalidUtf8 = true;
                return;
            }

            if (stream == CgcOutputStream.StandardOutput)
            {
                for (int index = 0; index < charsUsed; index++)
                {
                    ProcessStandardOutputCharacter(characters[index]);
                }
            }
        }
        catch (DecoderFallbackException)
        {
            _invalidUtf8 = true;
        }
    }

    private void FlushDecoder(CgcOutputStream stream)
    {
        if (_invalidUtf8)
        {
            return;
        }

        try
        {
            Decoder decoder = stream == CgcOutputStream.StandardOutput
                ? _standardOutputDecoder
                : _standardErrorDecoder;
            Span<char> characters = stackalloc char[2];
            decoder.Convert(ReadOnlySpan<byte>.Empty, characters, flush: true, out _, out int charsUsed, out _);
            if (stream == CgcOutputStream.StandardOutput)
            {
                for (int index = 0; index < charsUsed; index++)
                {
                    ProcessStandardOutputCharacter(characters[index]);
                }
            }
        }
        catch (DecoderFallbackException)
        {
            _invalidUtf8 = true;
        }
    }

    private void ProcessStandardOutputCharacter(char value)
    {
        if (value == '\r')
        {
            return;
        }

        if (value == '\n')
        {
            ProcessCompletedStandardOutputLine(_standardOutputLineBytes - 1);
            _standardOutputLineBytes = 0;
            _standardOutputLineMatcher.Reset();
            return;
        }

        _standardOutputLineMatcher.Observe(value);
        _lifecycleLineMatcher.Observe(value);
        _commandEvidenceRecognizer.Observe(value, _queryRegion != CgcQueryRegion.Inside);
        if (_queryRegion == CgcQueryRegion.Inside)
        {
            _structuralRecordRecognizer.Observe(value);
        }
    }

    private void ProcessCompletedStandardOutputLine(int byteCount)
    {
        CgcLineKind lineKind = _standardOutputLineMatcher.Complete();
        int lifecycleFact = _lifecycleLineMatcher.Complete();
        QualificationCommandEvidence? commandEvidence = _commandEvidenceRecognizer.Complete();
        if (commandEvidence is not null)
        {
            if (_commandEvidence.Count < _maximumQualifiedCommandEvidenceCount)
            {
                _commandEvidence.Add(commandEvidence);
            }
            else
            {
                _commandEvidenceOverflowed = true;
            }
        }

        switch (lifecycleFact)
        {
            case 0:
                _ranAsNonRoot = true;
                break;
            case 1:
                _indexedWholeRepository = true;
                break;
            case 2:
                _cleanupAttempted = true;
                break;
            case 3:
                _cleanupSucceeded = true;
                break;
            default:
                break;
        }

        _lifecycleLineMatcher.Reset();
        if (lineKind == CgcLineKind.Begin)
        {
            _stdoutBeginCount++;
            if (_queryRegion != CgcQueryRegion.None || _stdoutBeginCount != 1)
            {
                _markerOrderInvalid = true;
            }

            _queryRegion = CgcQueryRegion.Inside;
            _structuralRecordRecognizer.Reset();
            return;
        }

        if (lineKind == CgcLineKind.End)
        {
            _stdoutEndCount++;
            if (_queryRegion != CgcQueryRegion.Inside || _stdoutEndCount != 1)
            {
                _markerOrderInvalid = true;
            }
            else
            {
                _queryRegion = CgcQueryRegion.Ended;
            }

            _structuralRecordRecognizer.Reset();
            return;
        }

        if (_queryRegion == CgcQueryRegion.Inside)
        {
            _structuralRecordRecognizer.Complete(byteCount > MaximumStructuralRecordBytes);
        }
    }

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException("Streaming evidence cannot observe data after completion.");
        }
    }

    private enum CgcQueryRegion
    {
        None,
        Inside,
        Ended,
    }

    private enum CgcLineKind
    {
        Other,
        Begin,
        End,
    }

    private sealed class StreamingLineMatcher(string beginMarker, string endMarker)
    {
        private int _beginIndex;
        private int _endIndex;
        private bool _beginMatches = true;
        private bool _endMatches = true;

        internal void Observe(char value)
        {
            if (_beginMatches)
            {
                _beginMatches = _beginIndex < beginMarker.Length && beginMarker[_beginIndex++] == value;
            }

            if (_endMatches)
            {
                _endMatches = _endIndex < endMarker.Length && endMarker[_endIndex++] == value;
            }
        }

        internal CgcLineKind Complete()
        {
            if (_beginMatches && _beginIndex == beginMarker.Length)
            {
                return CgcLineKind.Begin;
            }

            return _endMatches && _endIndex == endMarker.Length
                ? CgcLineKind.End
                : CgcLineKind.Other;
        }

        internal void Reset()
        {
            _beginIndex = 0;
            _endIndex = 0;
            _beginMatches = true;
            _endMatches = true;
        }
    }

    private sealed class ExactSafeLineMatcher(params string[] expectedLines)
    {
        private int _candidateMask = (1 << expectedLines.Length) - 1;
        private int _length;

        internal void Observe(char value)
        {
            for (int index = 0; index < expectedLines.Length; index++)
            {
                if ((_candidateMask & (1 << index)) != 0 &&
                    (_length >= expectedLines[index].Length || expectedLines[index][_length] != value))
                {
                    _candidateMask &= ~(1 << index);
                }
            }

            _length++;
        }

        internal int Complete()
        {
            for (int index = 0; index < expectedLines.Length; index++)
            {
                if ((_candidateMask & (1 << index)) != 0 && expectedLines[index].Length == _length)
                {
                    return index;
                }
            }

            return -1;
        }

        internal void Reset()
        {
            _candidateMask = (1 << expectedLines.Length) - 1;
            _length = 0;
        }
    }

    /// <summary>
    /// Captures only the shell-generated lifecycle marker after its exact fixed
    /// prefix has matched. It never buffers provider output or query rows.
    /// </summary>
    private sealed class CgcCommandEvidenceRecognizer
    {
        private const string _prefix = "VULGATA_CGC_COMMAND=";
        private readonly char[] _safePayload = new char[208];
        private int _prefixIndex;
        private int _payloadLength;
        private bool _candidate = true;

        internal void Observe(char value, bool enabled)
        {
            if (!enabled || !_candidate)
            {
                return;
            }

            if (_prefixIndex < _prefix.Length)
            {
                if (_prefix[_prefixIndex] != value)
                {
                    _candidate = false;
                    return;
                }

                _prefixIndex++;
                return;
            }

            if (_payloadLength == _safePayload.Length || !IsSafeControlCharacter(value))
            {
                _candidate = false;
                return;
            }

            _safePayload[_payloadLength++] = value;
        }

        internal QualificationCommandEvidence? Complete()
        {
            QualificationCommandEvidence? evidence = null;
            if (_candidate && _prefixIndex == _prefix.Length && _payloadLength > 0)
            {
                ReadOnlySpan<char> payload = _safePayload.AsSpan(0, _payloadLength);
                Span<Range> ranges = stackalloc Range[4];
                if (payload.Split(ranges, '|') == 4)
                {
                    ReadOnlySpan<char> name = payload[ranges[0]];
                    ReadOnlySpan<char> exitCode = payload[ranges[1]];
                    ReadOnlySpan<char> stdoutSha256 = payload[ranges[2]];
                    ReadOnlySpan<char> stderrSha256 = payload[ranges[3]];
                    if (IsSafeCommandName(name) &&
                        int.TryParse(exitCode, System.Globalization.CultureInfo.InvariantCulture, out int parsedExitCode) &&
                        IsLowerSha256(stdoutSha256) &&
                        IsLowerSha256(stderrSha256))
                    {
                        evidence = new QualificationCommandEvidence(
                            name.ToString(),
                            parsedExitCode,
                            stdoutSha256.ToString(),
                            stderrSha256.ToString());
                    }
                }
            }

            Reset();
            return evidence;
        }

        private void Reset()
        {
            Array.Clear(_safePayload, 0, _safePayload.Length);
            _prefixIndex = 0;
            _payloadLength = 0;
            _candidate = true;
        }

        private static bool IsSafeControlCharacter(char value) =>
            value is >= 'a' and <= 'z' ||
            value is >= '0' and <= '9' ||
            value == '-' ||
            value == '|';

        private static bool IsSafeCommandName(ReadOnlySpan<char> value) =>
            value.Length is > 4 and <= 64 &&
            (value.StartsWith("cgc-", StringComparison.Ordinal) ||
             value.SequenceEqual("gateway-stop")) &&
            ContainsOnlyLowerControlCharacters(value, allowPipe: false);

        private static bool IsLowerSha256(ReadOnlySpan<char> value) =>
            value.Length == 64 &&
            ContainsOnlyLowerHex(value);

        private static bool ContainsOnlyLowerHex(ReadOnlySpan<char> value)
        {
            foreach (char character in value)
            {
                if (character is not (>= '0' and <= '9') && character is not (>= 'a' and <= 'f'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsOnlyLowerControlCharacters(ReadOnlySpan<char> value, bool allowPipe)
        {
            foreach (char character in value)
            {
                if (character is not (>= 'a' and <= 'z') &&
                    character is not (>= '0' and <= '9') &&
                    character != '-' &&
                    (!allowPipe || character != '|'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

internal enum CgcOutputStream
{
    StandardOutput = 0,
    StandardError = 1,
}

internal readonly record struct CgcObservationOutcome(bool TerminationRequired)
{
    internal static CgcObservationOutcome Continue { get; } = new(false);
    internal static CgcObservationOutcome OverflowTriggered { get; } = new(true);
    internal static CgcObservationOutcome OverflowAlreadyObserved { get; } = new(true);
}

/// <summary>
/// Keeps the overflow response independently testable: the 65,537th byte must
/// request process-tree termination and forced container cleanup exactly once.
/// </summary>
internal sealed class CgcOverflowTerminationSignal(Action requestTermination, Action requestForcedCleanup)
{
    private int _signaled;

    internal void Signal()
    {
        if (Interlocked.Exchange(ref _signaled, 1) != 0)
        {
            return;
        }

        requestTermination();
        requestForcedCleanup();
    }
}

internal sealed record CgcStreamEvidence(long Bytes, string Sha256);

internal sealed record CgcStreamingEvidence(
    bool IsValidUtf8,
    bool Overflowed,
    bool TerminationRequested,
    long ObservedTotalBytes,
    CgcStreamEvidence StandardOutput,
    CgcStreamEvidence StandardError,
    string CanonicalSummarySha256,
    int StdoutBeginCount,
    int StdoutEndCount,
    bool HasExactlyOneOrderedStdoutMarkerPair,
    bool RanAsNonRoot,
    bool IndexedWholeRepository,
    bool CleanupSucceeded,
    bool CleanupAttempted,
    bool ContainerCleanupForced,
    bool HasValidRecordGrammar,
    bool HasValidStructuralRecord,
    int ValidRecordCount,
    int InvalidOrOversizedRecordCount,
    IReadOnlyDictionary<string, int> RelationshipKindCounts,
    bool HasAllRequiredRelationshipKinds,
    bool HasFilteredTargetCandidate,
    bool HasPositiveOrderedRange,
    bool HasNonEmptyEndpointIdentity,
    bool AllStructuralFactsCorrelatedWithinValidRecords,
    IReadOnlyList<QualificationCommandEvidence> Commands,
    bool CommandEvidenceOverflowed);

internal sealed class CgcStructuralRecordRecognizer
{
    private static readonly string[] _fieldNames =
    [
        "relationship_kind",
        "source_path",
        "source_symbol",
        "source_start_line",
        "source_end_line",
        "source_identity",
        "target_path",
        "target_symbol",
        "target_start_line",
        "target_end_line",
        "target_identity",
    ];
    private static readonly string[] _relationshipKinds = ["CALLS", "CONTAINS", "IMPORTS", "INHERITS"];
    private const string _filteredTargetPath = "src/Filtered/GeneratedClient.g.cs";
    private readonly int[] _relationshipKindCounts = new int[_relationshipKinds.Length];
    private int _fieldIndex;
    private int _keyCandidateMask;
    private int _keyLength;
    private CgcValueState _valueState;
    private bool _rowInvalid;
    private bool _rowHasValue;
    private bool _pathValueNonEmpty;
    private bool _pathValueRelative;
    private bool _pathPreviousWasPeriod;
    private int _pathValueLength;
    private bool _textValueNonEmpty;
    private bool _filteredTargetMatches;
    private int _filteredTargetPosition;
    private int _relationshipCandidateMask;
    private int _relationshipLength;
    private bool _relationshipKindObserved;
    private int _rangeValue;
    private bool _rangeValueHasDigits;
    private int _sourceStart;
    private int _sourceEnd;
    private int _targetStart;
    private int _targetEnd;
    private bool _sourcePathValid;
    private bool _sourceSymbolValid;
    private bool _sourceIdentityValid;
    private bool _targetPathValid;
    private bool _targetSymbolValid;
    private bool _targetIdentityValid;

    internal int ValidRecordCount { get; private set; }

    internal int InvalidOrOversizedRecordCount { get; private set; }

    internal bool HasFilteredTargetCandidate { get; private set; }

    internal bool HasPositiveOrderedRange { get; private set; }

    internal bool HasNonEmptyEndpointIdentity { get; private set; }

    internal bool AllFactsCorrelatedWithinValidRecords { get; private set; }

    internal void Observe(char value)
    {
        if (_rowInvalid)
        {
            return;
        }

        if (_valueState == CgcValueState.Key)
        {
            ObserveKey(value);
            return;
        }

        if (value == '|')
        {
            CompleteValue();
            return;
        }

        ObserveValue(value);
    }

    internal void Complete(bool oversized)
    {
        if (!_rowHasValue)
        {
            InvalidOrOversizedRecordCount++;
            Reset();
            return;
        }

        if (_valueState != CgcValueState.Key)
        {
            CompleteValue();
        }

        bool valid = !oversized &&
            !_rowInvalid &&
            _fieldIndex == _fieldNames.Length &&
            _sourcePathValid &&
            _sourceSymbolValid &&
            _sourceIdentityValid &&
            _targetPathValid &&
            _targetSymbolValid &&
            _targetIdentityValid &&
            _sourceStart > 0 && _sourceEnd >= _sourceStart &&
            _targetStart > 0 && _targetEnd >= _targetStart &&
            _relationshipKindObserved;

        if (valid)
        {
            ValidRecordCount++;
            HasFilteredTargetCandidate |= _filteredTargetMatches;
            HasPositiveOrderedRange = true;
            HasNonEmptyEndpointIdentity = true;
            AllFactsCorrelatedWithinValidRecords = true;
        }
        else
        {
            InvalidOrOversizedRecordCount++;
        }

        Reset();
    }

    internal void Reset()
    {
        _fieldIndex = 0;
        _keyCandidateMask = (1 << _fieldNames.Length) - 1;
        _keyLength = 0;
        _valueState = CgcValueState.Key;
        _rowInvalid = false;
        _rowHasValue = false;
        _pathValueNonEmpty = false;
        _pathValueRelative = true;
        _pathPreviousWasPeriod = false;
        _pathValueLength = 0;
        _textValueNonEmpty = false;
        _filteredTargetMatches = false;
        _filteredTargetPosition = 0;
        _relationshipCandidateMask = (1 << _relationshipKinds.Length) - 1;
        _relationshipLength = 0;
        _relationshipKindObserved = false;
        _rangeValue = 0;
        _rangeValueHasDigits = false;
        _sourceStart = 0;
        _sourceEnd = 0;
        _targetStart = 0;
        _targetEnd = 0;
        _sourcePathValid = false;
        _sourceSymbolValid = false;
        _sourceIdentityValid = false;
        _targetPathValid = false;
        _targetSymbolValid = false;
        _targetIdentityValid = false;
    }

    internal IReadOnlyDictionary<string, int> GetRelationshipKindCounts()
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        for (int index = 0; index < _relationshipKinds.Length; index++)
        {
            counts.Add(_relationshipKinds[index], _relationshipKindCounts[index]);
        }

        return new ReadOnlyDictionary<string, int>(counts);
    }

    private void ObserveKey(char value)
    {
        if (value == '=')
        {
            int selectedField = FindSelectedCandidate(_fieldNames, _keyCandidateMask, _keyLength);
            if (selectedField != _fieldIndex)
            {
                _rowInvalid = true;
                return;
            }

            _valueState = CgcValueState.Value;
            _rowHasValue = true;
            ResetValueState();
            return;
        }

        if (value is '|' or '\n' or '\r')
        {
            _rowInvalid = true;
            return;
        }

        _keyCandidateMask = AdvanceCandidates(_fieldNames, _keyCandidateMask, _keyLength, value);
        _keyLength++;
    }

    private void ObserveValue(char value)
    {
        if (_fieldIndex == 0)
        {
            _relationshipCandidateMask = AdvanceCandidates(
                _relationshipKinds,
                _relationshipCandidateMask,
                _relationshipLength,
                value);
            _relationshipLength++;
            return;
        }

        if (IsPathField(_fieldIndex))
        {
            _pathValueNonEmpty = true;
            if (_pathValueLength == 0 && value is ('/' or '\\'))
            {
                _pathValueRelative = false;
            }

            if (value is ('\\' or ':'))
            {
                _pathValueRelative = false;
            }

            if (_pathPreviousWasPeriod && value == '.')
            {
                _pathValueRelative = false;
            }

            _pathPreviousWasPeriod = value == '.';
            _pathValueLength++;
            if (_fieldIndex == 6)
            {
                if (_filteredTargetPosition >= _filteredTargetPath.Length || _filteredTargetPath[_filteredTargetPosition] != value)
                {
                    _filteredTargetMatches = false;
                }

                _filteredTargetPosition++;
            }

            return;
        }

        if (IsTextField(_fieldIndex))
        {
            _textValueNonEmpty |= !char.IsWhiteSpace(value);
            return;
        }

        if (value is >= '0' and <= '9')
        {
            int digit = value - '0';
            if (_rangeValue > (int.MaxValue - digit) / 10)
            {
                _rowInvalid = true;
                return;
            }

            _rangeValue = (_rangeValue * 10) + digit;
            _rangeValueHasDigits = true;
            return;
        }

        _rowInvalid = true;
    }

    private void CompleteValue()
    {
        if (_rowInvalid || _fieldIndex >= _fieldNames.Length)
        {
            _rowInvalid = true;
            return;
        }

        switch (_fieldIndex)
        {
            case 0:
                {
                    int relationshipKind = FindSelectedCandidate(_relationshipKinds, _relationshipCandidateMask, _relationshipLength);
                    if (relationshipKind < 0)
                    {
                        _rowInvalid = true;
                        return;
                    }

                    _relationshipKindCounts[relationshipKind]++;
                    _relationshipKindObserved = true;
                    break;
                }
            case 1:
                _sourcePathValid = _pathValueNonEmpty && _pathValueRelative;
                break;
            case 2:
                _sourceSymbolValid = _textValueNonEmpty;
                break;
            case 3:
                _sourceStart = _rangeValueHasDigits ? _rangeValue : 0;
                break;
            case 4:
                _sourceEnd = _rangeValueHasDigits ? _rangeValue : 0;
                break;
            case 5:
                _sourceIdentityValid = _textValueNonEmpty;
                break;
            case 6:
                _targetPathValid = _pathValueNonEmpty && _pathValueRelative;
                _filteredTargetMatches &= _filteredTargetPosition == _filteredTargetPath.Length;
                break;
            case 7:
                _targetSymbolValid = _textValueNonEmpty;
                break;
            case 8:
                _targetStart = _rangeValueHasDigits ? _rangeValue : 0;
                break;
            case 9:
                _targetEnd = _rangeValueHasDigits ? _rangeValue : 0;
                break;
            case 10:
                _targetIdentityValid = _textValueNonEmpty;
                break;
            default:
                _rowInvalid = true;
                return;
        }

        _fieldIndex++;
        _valueState = CgcValueState.Key;
        _keyCandidateMask = (1 << _fieldNames.Length) - 1;
        _keyLength = 0;
    }

    private void ResetValueState()
    {
        _pathValueNonEmpty = false;
        _pathValueRelative = true;
        _pathPreviousWasPeriod = false;
        _pathValueLength = 0;
        _textValueNonEmpty = false;
        _rangeValue = 0;
        _rangeValueHasDigits = false;
        if (_fieldIndex == 6)
        {
            _filteredTargetMatches = true;
            _filteredTargetPosition = 0;
        }
        else if (_fieldIndex == 0)
        {
            _relationshipCandidateMask = (1 << _relationshipKinds.Length) - 1;
            _relationshipLength = 0;
        }
    }

    private static int AdvanceCandidates(IReadOnlyList<string> candidates, int candidateMask, int position, char value)
    {
        for (int index = 0; index < candidates.Count; index++)
        {
            if ((candidateMask & (1 << index)) != 0 &&
                (position >= candidates[index].Length || candidates[index][position] != value))
            {
                candidateMask &= ~(1 << index);
            }
        }

        return candidateMask;
    }

    private static int FindSelectedCandidate(IReadOnlyList<string> candidates, int candidateMask, int length)
    {
        int selected = -1;
        for (int index = 0; index < candidates.Count; index++)
        {
            if ((candidateMask & (1 << index)) != 0 && candidates[index].Length == length)
            {
                if (selected >= 0)
                {
                    return -1;
                }

                selected = index;
            }
        }

        return selected;
    }

    private static bool IsPathField(int fieldIndex) => fieldIndex is 1 or 6;

    private static bool IsTextField(int fieldIndex) => fieldIndex is 2 or 5 or 7 or 10;

    private enum CgcValueState
    {
        Key,
        Value,
    }
}
