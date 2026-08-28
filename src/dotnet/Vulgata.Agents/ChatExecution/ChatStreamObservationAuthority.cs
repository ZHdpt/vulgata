using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.ChatExecution;

internal sealed record ChatStreamRuntimeObservation(
    string RuntimeObservationId,
    int ObservationSequence,
    string RequestId,
    string AttemptKind,
    string ProviderIdentity,
    string ProviderApi,
    string ProviderConfigurationFingerprint,
    string CanonicalStreamingRequestFingerprint,
    bool RequestStreamMode,
    string ResponseHeaderDisposition,
    int? ResponseStatusCode,
    string? ResponseMediaType,
    string? NegotiatedProtocol,
    DateTimeOffset? ResponseHeadersAtUtc,
    string FirstPhysicalReadDisposition,
    DateTimeOffset? FirstPhysicalReadAtUtc,
    DateTimeOffset? PhysicalEofAtUtc,
    string PhysicalEofDisposition,
    bool StreamingTransport,
    DateTimeOffset StreamStartedAtUtc,
    DateTimeOffset? FirstDataAtUtc,
    DateTimeOffset? TerminalAtUtc,
    long? TimeToFirstDataMilliseconds,
    int RawReadCount,
    int MaximumRawReadByteCount,
    int RawBodyByteCount,
    int MaximumSseLineByteCount,
    int MaximumSseEventByteCount,
    int ProviderChunkCount,
    int MaximumReceivedProviderChunkByteCount,
    int AssembledReceivedProviderResponseByteCount,
    int AnswerDeltaCount,
    int MaximumAnswerDeltaByteCount,
    int AssembledAnswerByteCount,
    int TerminalEventCount,
    int InactivityLimitMilliseconds,
    DateTimeOffset OverallDeadlineAtUtc,
    string TransportOutcome,
    string? RejectionCategory);

internal sealed class ChatStreamRuntimeObservationRecorder :
    IChatStreamCompletionSummaryRecorder
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<ChatStreamRuntimeObservation>> _observations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChatStreamCompletionSummary> _summaries =
        new(StringComparer.Ordinal);
    private int _observationSequence;

    internal ChatStreamRuntimeObservation Record(
        Func<string, int, ChatStreamRuntimeObservation> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        int sequence = Interlocked.Increment(ref _observationSequence);
        ChatStreamRuntimeObservation observation = create(
            $"runtime-{Guid.NewGuid():N}", sequence);
        lock (_gate)
        {
            if (!_observations.TryGetValue(observation.RequestId, out var values))
            {
                values = [];
                _observations.Add(observation.RequestId, values);
            }

            values.Add(observation);
        }

        return observation;
    }

    public IReadOnlyList<string> GetRuntimeObservationIds(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return [];
        }

        lock (_gate)
        {
            return _observations.TryGetValue(requestId, out var values)
                ? values.Select(value => value.RuntimeObservationId).ToArray()
                : [];
        }
    }

    internal IReadOnlyList<ChatStreamRuntimeObservation> GetRuntimeObservations(
        string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return [];
        }

        lock (_gate)
        {
            return _observations.TryGetValue(requestId, out var values)
                ? values.ToArray()
                : [];
        }
    }

    public void RecordCompletion(ChatStreamCompletionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        lock (_gate)
        {
            _summaries[summary.RequestId] = summary;
        }
    }
}
