using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

    private const string _nonRootMarker = "VULGATA_CGC_QUALIFICATION_NON_ROOT=SUCCEEDED";
    private const string _indexMarker = "VULGATA_CGC_QUALIFICATION_INDEX=SUCCEEDED";
    private const string _cleanupAttemptedMarker = "VULGATA_CGC_QUALIFICATION_CLEANUP=ATTEMPTED";
    private const string _cleanupMarker = "VULGATA_CGC_QUALIFICATION_CLEANUP=SUCCEEDED";
    private readonly object _gate = new();
    private readonly Action? _overflowSignal;
    private readonly Decoder _standardOutputDecoder = new UTF8Encoding(false, true).GetDecoder();
    private readonly Decoder _standardErrorDecoder = new UTF8Encoding(false, true).GetDecoder();
    private readonly Decoder _healthDecoder = new UTF8Encoding(false, true).GetDecoder();
    private readonly Decoder _queryDecoder = new UTF8Encoding(false, true).GetDecoder();
    private readonly IncrementalHash _standardOutputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly IncrementalHash _standardErrorHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly IncrementalHash _healthHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly IncrementalHash _queryHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly ExactSafeLineMatcher _lifecycleLineMatcher = new(_nonRootMarker, _indexMarker, _cleanupAttemptedMarker, _cleanupMarker);
    private readonly CgcCommandEvidenceRecognizer _commandEvidenceRecognizer = new();
    private readonly List<QualificationCommandEvidence> _commandEvidence = [];
    private readonly CgcHttpJsonRecognizer _httpJsonRecognizer = new(CgcQualificationRunner.StaticStructuralQuery);
    private long _standardOutputBytes;
    private long _standardErrorBytes;
    private long _healthBytes;
    private long _queryBytes;
    private int _standardOutputLineBytes;
    private bool _ranAsNonRoot;
    private bool _indexedWholeRepository;
    private bool _cleanupAttempted;
    private bool _cleanupSucceeded;
    private bool _commandEvidenceOverflowed;
    private bool _invalidUtf8;
    private bool _overflowed;
    private bool _completed;
    private CgcStreamingEvidence? _evidence;

    internal CgcStreamingEvidenceObserver(Action? overflowSignal = null)
    {
        _overflowSignal = overflowSignal;
    }

    internal CgcObservationOutcome Observe(CgcOutputStream stream, ReadOnlySpan<byte> bytes)
    {
        if (stream is not (CgcOutputStream.StandardOutput or CgcOutputStream.StandardError or
            CgcOutputStream.HttpHealthResponse or CgcOutputStream.HttpQueryResponse))
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
                long observedBefore = ObservedTotalBytes;
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
            FlushDecoder(CgcOutputStream.HttpHealthResponse);
            FlushDecoder(CgcOutputStream.HttpQueryResponse);
            _httpJsonRecognizer.Complete();

            _completed = true;
            IReadOnlyDictionary<string, int> relationshipKindCounts = _httpJsonRecognizer.GetRelationshipKindCounts();
            bool queryCompleted = !_overflowed && !_invalidUtf8 && _httpJsonRecognizer.IsComplete;
            bool hasValidRecordGrammar = _httpJsonRecognizer.ValidRecordCount > 0 &&
                _httpJsonRecognizer.InvalidOrOversizedRecordCount == 0 && queryCompleted;
            string stdoutSha256 = Convert.ToHexString(_standardOutputHash.GetHashAndReset()).ToLowerInvariant();
            string stderrSha256 = Convert.ToHexString(_standardErrorHash.GetHashAndReset()).ToLowerInvariant();
            string healthSha256 = Convert.ToHexString(_healthHash.GetHashAndReset()).ToLowerInvariant();
            string querySha256 = Convert.ToHexString(_queryHash.GetHashAndReset()).ToLowerInvariant();
            bool containerCleanupForced = _overflowed;
            string canonicalSummarySha256 = CreateCanonicalSummarySha256(
                stdoutSha256,
                stderrSha256,
                relationshipKindCounts,
                queryCompleted,
                hasValidRecordGrammar,
                _cleanupAttempted,
                containerCleanupForced);
            _evidence = new CgcStreamingEvidence(
                IsValidUtf8: !_invalidUtf8,
                Overflowed: _overflowed,
                TerminationRequested: _overflowed,
                ObservedTotalBytes: ObservedTotalBytes,
                StandardOutput: new CgcStreamEvidence(_standardOutputBytes, stdoutSha256),
                StandardError: new CgcStreamEvidence(_standardErrorBytes, stderrSha256),
                CanonicalSummarySha256: canonicalSummarySha256,
                StdoutBeginCount: 0,
                StdoutEndCount: 0,
                HasExactlyOneOrderedStdoutMarkerPair: queryCompleted,
                RanAsNonRoot: _ranAsNonRoot,
                IndexedWholeRepository: _indexedWholeRepository,
                CleanupSucceeded: _cleanupSucceeded,
                CleanupAttempted: _cleanupAttempted,
                ContainerCleanupForced: containerCleanupForced,
                HasValidRecordGrammar: hasValidRecordGrammar,
                HasValidStructuralRecord: hasValidRecordGrammar,
                ValidRecordCount: _httpJsonRecognizer.ValidRecordCount,
                InvalidOrOversizedRecordCount: _httpJsonRecognizer.InvalidOrOversizedRecordCount,
                RelationshipKindCounts: relationshipKindCounts,
                HasAllRequiredRelationshipKinds: relationshipKindCounts.Values.All(count => count > 0),
                HasFilteredTargetCandidate: _httpJsonRecognizer.HasFilteredTargetCandidate,
                HasPositiveOrderedRange: _httpJsonRecognizer.HasPositiveOrderedRange,
                HasNonEmptyEndpointIdentity: _httpJsonRecognizer.HasNonEmptyEndpointIdentity,
                AllStructuralFactsCorrelatedWithinValidRecords: _httpJsonRecognizer.AllFactsCorrelatedWithinValidRecords,
                Commands: _commandEvidence.ToArray(),
                CommandEvidenceOverflowed: _commandEvidenceOverflowed,
                HealthResponse: new CgcStreamEvidence(_healthBytes, healthSha256),
                HttpResponse: new CgcStreamEvidence(_queryBytes, querySha256));
            return _evidence;
        }
    }

    internal string HttpDiagnostic => _httpJsonRecognizer.Diagnostic;

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
            $"valid={_httpJsonRecognizer.ValidRecordCount}",
            $"invalidOrOversized={_httpJsonRecognizer.InvalidOrOversizedRecordCount}",
            $"CALLS={relationshipKindCounts["CALLS"]}",
            $"CONTAINS={relationshipKindCounts["CONTAINS"]}",
            $"IMPORTS={relationshipKindCounts["IMPORTS"]}",
            $"INHERITS={relationshipKindCounts["INHERITS"]}",
            $"allRequiredRelationshipKindsObserved={ToLowerBoolean(relationshipKindCounts.Values.All(count => count > 0))}",
            $"allStructuralFactsCorrelatedWithinValidRecords={ToLowerBoolean(_httpJsonRecognizer.AllFactsCorrelatedWithinValidRecords)}",
            $"hasFilteredTargetCandidate={ToLowerBoolean(_httpJsonRecognizer.HasFilteredTargetCandidate)}",
            $"hasNonEmptyEndpointIdentity={ToLowerBoolean(_httpJsonRecognizer.HasNonEmptyEndpointIdentity)}",
            $"hasPositiveOrderedRange={ToLowerBoolean(_httpJsonRecognizer.HasPositiveOrderedRange)}",
            $"hasValidRecordGrammar={ToLowerBoolean(hasValidRecordGrammar)}",
            $"queryCompleted={ToLowerBoolean(queryCompleted)}");
        string summary = string.Join(
            '|',
            "1.2",
            _standardOutputBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _standardErrorBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            (_standardOutputBytes + _standardErrorBytes).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ToLowerBoolean(_overflowed),
            ToLowerBoolean(_overflowed),
            ToLowerBoolean(containerCleanupForced),
            stdoutSha256,
            stderrSha256,
            "vulgata-cgc-http-json-v1,vulgata-cgc-streaming-evidence-v2",
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
        _healthHash.Dispose();
        _queryHash.Dispose();
    }

    private long ObservedTotalBytes => _standardOutputBytes + _standardErrorBytes + _healthBytes + _queryBytes;

    private void AddByte(CgcOutputStream stream, byte value)
    {
        if (stream == CgcOutputStream.StandardOutput)
        {
            _standardOutputBytes++;
            _standardOutputHash.AppendData([value]);
            _standardOutputLineBytes++;
        }
        else if (stream == CgcOutputStream.StandardError)
        {
            _standardErrorBytes++;
            _standardErrorHash.AppendData([value]);
        }
        else if (stream == CgcOutputStream.HttpHealthResponse)
        {
            _healthBytes++;
            _healthHash.AppendData([value]);
        }
        else
        {
            _queryBytes++;
            _queryHash.AppendData([value]);
            DecodeByte(stream, value);
            _httpJsonRecognizer.Observe(value);
            return;
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
            Decoder decoder = stream switch
            {
                CgcOutputStream.StandardOutput => _standardOutputDecoder,
                CgcOutputStream.StandardError => _standardErrorDecoder,
                CgcOutputStream.HttpHealthResponse => _healthDecoder,
                CgcOutputStream.HttpQueryResponse => _queryDecoder,
                _ => throw new ArgumentOutOfRangeException(nameof(stream)),
            };
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
            Decoder decoder = stream switch
            {
                CgcOutputStream.StandardOutput => _standardOutputDecoder,
                CgcOutputStream.StandardError => _standardErrorDecoder,
                CgcOutputStream.HttpHealthResponse => _healthDecoder,
                CgcOutputStream.HttpQueryResponse => _queryDecoder,
                _ => throw new ArgumentOutOfRangeException(nameof(stream)),
            };
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
            return;
        }

        _lifecycleLineMatcher.Observe(value);
        _commandEvidenceRecognizer.Observe(value, enabled: true);
    }

    private void ProcessCompletedStandardOutputLine(int byteCount)
    {
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
    }

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException("Streaming evidence cannot observe data after completion.");
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
    HttpHealthResponse = 2,
    HttpQueryResponse = 3,
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
    bool CommandEvidenceOverflowed,
    CgcStreamEvidence? HealthResponse = null,
    CgcStreamEvidence? HttpResponse = null);

/// <summary>
/// Incremental, qualification-only parser for the pinned HTTP response candidate.
/// It retains only an incomplete JSON token, never a response document or result set.
/// </summary>
internal sealed class CgcHttpJsonRecognizer(string expectedQuery)
{
    private static readonly string[] _aliases =
    [
        "relationship_kind", "source_path", "source_symbol", "source_start_line",
        "source_end_line", "source_identity", "target_path", "target_symbol",
        "target_start_line", "target_end_line", "target_identity", "filtered_target",
    ];
    private static readonly string[] _relationshipKinds = ["CALLS", "CONTAINS", "IMPORTS", "INHERITS"];
    private const string _filteredTargetPath = "src/Filtered/GeneratedClient.g.cs";
    private readonly byte[] _incompleteToken = new byte[8_192];
    private readonly int[] _relationshipKindCounts = new int[4];
    private JsonReaderState _readerState;
    private int _incompleteCount;
    private long _fedBytes;
    private long _recordStart;
    private int _phase;
    private int _declaredRecordCount = -1;
    private int _relationshipKind = -1;
    private int _sourceStart;
    private int _sourceEnd;
    private int _targetStart;
    private int _targetEnd;
    private bool _targetStartNull;
    private bool _targetEndNull;
    private bool _sourcePathValid;
    private bool _sourceSymbolValid;
    private bool _sourceIdentityValid;
    private bool _targetPathValid;
    private bool _targetSymbolValid;
    private bool _targetIdentityValid;
    private bool _targetIsFilteredPath;
    private bool _filteredTarget;
    private bool _recordOversized;
    private bool _invalid;
    private bool _finalized;

    internal int ValidRecordCount { get; private set; }

    internal int InvalidOrOversizedRecordCount { get; private set; }

    internal bool HasFilteredTargetCandidate { get; private set; }

    internal bool HasPositiveOrderedRange { get; private set; }

    internal bool HasNonEmptyEndpointIdentity { get; private set; }

    internal bool AllFactsCorrelatedWithinValidRecords { get; private set; }

    internal bool IsComplete => _finalized && !_invalid && _phase == 18 &&
        _declaredRecordCount == ValidRecordCount;

    internal string Diagnostic => $"phase={_phase};invalid={_invalid};carry={_incompleteCount};declared={_declaredRecordCount};valid={ValidRecordCount}";

    internal void Observe(byte value)
    {
        if (_finalized || _invalid)
        {
            return;
        }

        _fedBytes++;
        if (_phase is >= 100 and <= 124 && _fedBytes - _recordStart > CgcStreamingEvidenceObserver.MaximumStructuralRecordBytes)
        {
            _recordOversized = true;
        }

        if (_incompleteCount == _incompleteToken.Length)
        {
            _invalid = true;
            return;
        }

        _incompleteToken[_incompleteCount++] = value;
        Consume(isFinalBlock: false);
    }

    internal void Complete()
    {
        if (_finalized)
        {
            return;
        }

        Consume(isFinalBlock: true);
        _finalized = true;
        if (_phase != 18 || _incompleteCount != 0 || _declaredRecordCount != ValidRecordCount)
        {
            _invalid = true;
        }
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

    private void Consume(bool isFinalBlock)
    {
        if (_invalid)
        {
            return;
        }

        try
        {
            Utf8JsonReader reader = new(
                _incompleteToken.AsSpan(0, _incompleteCount),
                isFinalBlock,
                _readerState);
            while (reader.Read())
            {
                ProcessToken(ref reader, _fedBytes - _incompleteCount + reader.TokenStartIndex);
                if (_invalid)
                {
                    break;
                }
            }

            int consumed = checked((int)reader.BytesConsumed);
            _readerState = reader.CurrentState;
            if (consumed > 0)
            {
                _incompleteToken.AsSpan(consumed, _incompleteCount - consumed).CopyTo(_incompleteToken);
                _incompleteToken.AsSpan(_incompleteCount - consumed, consumed).Clear();
                _incompleteCount -= consumed;
            }
        }
        catch (JsonException)
        {
            _invalid = true;
        }
    }

    private void ProcessToken(ref Utf8JsonReader reader, long absoluteTokenStart)
    {
        if (_phase == 18)
        {
            _invalid = true;
            return;
        }

        if (_phase is >= 100 and <= 124)
        {
            ProcessRecordToken(ref reader);
            return;
        }

        switch (_phase)
        {
            case 0:
                Require(reader.TokenType == JsonTokenType.StartObject);
                _phase = 1;
                break;
            case 1:
                RequireProperty(ref reader, "status", 2);
                break;
            case 2:
                Require(reader.TokenType == JsonTokenType.String && reader.ValueTextEquals("ok"));
                _phase = 3;
                break;
            case 3:
                RequireProperty(ref reader, "error", 4);
                break;
            case 4:
                Require(reader.TokenType == JsonTokenType.Null);
                _phase = 5;
                break;
            case 5:
                RequireProperty(ref reader, "data", 6);
                break;
            case 6:
                Require(reader.TokenType == JsonTokenType.StartObject);
                _phase = 7;
                break;
            case 7:
                RequireProperty(ref reader, "success", 8);
                break;
            case 8:
                Require(reader.TokenType == JsonTokenType.True);
                _phase = 9;
                break;
            case 9:
                RequireProperty(ref reader, "query", 10);
                break;
            case 10:
                Require(reader.TokenType == JsonTokenType.String && reader.ValueTextEquals(expectedQuery));
                _phase = 11;
                break;
            case 11:
                RequireProperty(ref reader, "record_count", 12);
                break;
            case 12:
                Require(reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out _declaredRecordCount) && _declaredRecordCount >= 0);
                _phase = 13;
                break;
            case 13:
                RequireProperty(ref reader, "results", 14);
                break;
            case 14:
                Require(reader.TokenType == JsonTokenType.StartArray);
                _phase = 15;
                break;
            case 15:
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    _phase = 16;
                }
                else
                {
                    Require(reader.TokenType == JsonTokenType.StartObject);
                    _recordStart = absoluteTokenStart;
                    ResetRecord();
                    _phase = 100;
                }

                break;
            case 16:
                Require(reader.TokenType == JsonTokenType.EndObject);
                _phase = 17;
                break;
            case 17:
                Require(reader.TokenType == JsonTokenType.EndObject);
                _phase = 18;
                break;
            default:
                _invalid = true;
                break;
        }
    }

    private void ProcessRecordToken(ref Utf8JsonReader reader)
    {
        if (_phase == 124)
        {
            Require(reader.TokenType == JsonTokenType.EndObject);
            CompleteRecord();
            _phase = 15;
            return;
        }

        int relativePhase = _phase - 100;
        int aliasIndex = relativePhase / 2;
        if ((relativePhase & 1) == 0)
        {
            RequireProperty(ref reader, _aliases[aliasIndex], _phase + 1);
            return;
        }

        ValidateRecordValue(aliasIndex, ref reader);
        _phase++;
    }

    private void ValidateRecordValue(int aliasIndex, ref Utf8JsonReader reader)
    {
        switch (aliasIndex)
        {
            case 0:
                _relationshipKind = MatchRelationshipKind(ref reader);
                Require(_relationshipKind >= 0);
                break;
            case 1:
                _sourcePathValid = IsNormalizedRelativePath(ref reader);
                Require(_sourcePathValid);
                break;
            case 2:
                _sourceSymbolValid = IsNonEmptyString(ref reader);
                Require(_sourceSymbolValid);
                break;
            case 3:
                Require(TryGetPositiveInt(ref reader, out _sourceStart));
                break;
            case 4:
                Require(TryGetPositiveInt(ref reader, out _sourceEnd));
                break;
            case 5:
                _sourceIdentityValid = IsNonEmptyString(ref reader);
                Require(_sourceIdentityValid);
                break;
            case 6:
                _targetPathValid = IsNormalizedRelativePath(ref reader);
                _targetIsFilteredPath = reader.TokenType == JsonTokenType.String && reader.ValueTextEquals(_filteredTargetPath);
                Require(_targetPathValid);
                break;
            case 7:
                _targetSymbolValid = IsNonEmptyString(ref reader);
                Require(_targetSymbolValid);
                break;
            case 8:
                _targetStartNull = reader.TokenType == JsonTokenType.Null;
                Require(_targetStartNull || TryGetPositiveInt(ref reader, out _targetStart));
                break;
            case 9:
                _targetEndNull = reader.TokenType == JsonTokenType.Null;
                Require(_targetEndNull || TryGetPositiveInt(ref reader, out _targetEnd));
                break;
            case 10:
                _targetIdentityValid = IsNonEmptyString(ref reader);
                Require(_targetIdentityValid);
                break;
            case 11:
                Require(reader.TokenType is JsonTokenType.True or JsonTokenType.False);
                _filteredTarget = reader.TokenType == JsonTokenType.True;
                break;
            default:
                _invalid = true;
                break;
        }
    }

    private void CompleteRecord()
    {
        bool ordinaryRanges = !_targetStartNull && !_targetEndNull && _targetEnd >= _targetStart;
        bool filteredRanges = _targetStartNull && _targetEndNull;
        bool relationshipSpecificRanges = _relationshipKind != 2 ||
            (_sourceStart == _sourceEnd && _targetStart == _targetEnd);
        bool valid = !_recordOversized &&
            _relationshipKind >= 0 &&
            _sourcePathValid && _sourceSymbolValid && _sourceIdentityValid &&
            _targetPathValid && _targetSymbolValid && _targetIdentityValid &&
            _sourceEnd >= _sourceStart &&
            ((!_filteredTarget && ordinaryRanges && relationshipSpecificRanges) ||
             (_filteredTarget && _relationshipKind == 0 && _targetIsFilteredPath && filteredRanges));
        if (!valid)
        {
            InvalidOrOversizedRecordCount++;
            _invalid = true;
            return;
        }

        ValidRecordCount++;
        _relationshipKindCounts[_relationshipKind]++;
        HasFilteredTargetCandidate |= _filteredTarget;
        HasPositiveOrderedRange = true;
        HasNonEmptyEndpointIdentity = true;
        AllFactsCorrelatedWithinValidRecords = true;
    }

    private void ResetRecord()
    {
        _relationshipKind = -1;
        _sourceStart = _sourceEnd = _targetStart = _targetEnd = 0;
        _targetStartNull = _targetEndNull = false;
        _sourcePathValid = _sourceSymbolValid = _sourceIdentityValid = false;
        _targetPathValid = _targetSymbolValid = _targetIdentityValid = false;
        _targetIsFilteredPath = _filteredTarget = _recordOversized = false;
    }

    private int MatchRelationshipKind(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String || reader.ValueIsEscaped)
        {
            return -1;
        }

        for (int index = 0; index < _relationshipKinds.Length; index++)
        {
            if (reader.ValueTextEquals(_relationshipKinds[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsNonEmptyString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String && !reader.ValueIsEscaped && reader.ValueSpan.Length > 0;

    private static bool IsNormalizedRelativePath(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String || reader.ValueIsEscaped || reader.ValueSpan.IsEmpty)
        {
            return false;
        }

        ReadOnlySpan<byte> value = reader.ValueSpan;
        if (value[0] is (byte)'/' or (byte)'\\')
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] is (byte)'\\' or (byte)':' ||
                (value[index] == (byte)'.' && index + 1 < value.Length && value[index + 1] == (byte)'.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPositiveInt(ref Utf8JsonReader reader, out int value)
    {
        value = 0;
        return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out value) && value > 0;
    }

    private void RequireProperty(ref Utf8JsonReader reader, string name, int nextPhase)
    {
        Require(reader.TokenType == JsonTokenType.PropertyName && !reader.ValueIsEscaped && reader.ValueTextEquals(name));
        _phase = nextPhase;
    }

    private void Require(bool condition)
    {
        if (!condition)
        {
            _invalid = true;
        }
    }
}
