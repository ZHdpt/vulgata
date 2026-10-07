using System.Buffers;
using System.Text;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.ChatExecution.October;

/// <summary>One original monotonic lifetime and accounting scope, shared across every round and continuation.</summary>
public sealed class OctoberChatBudget
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly long _started;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<int, long> _roundBytes = [];
    private bool _stopped;
    private int _providerRounds;
    private int _toolCalls;
    private int _activeTools;
    private long _toolResultBytes;
    private long _outputBytes;
    private long _sseBytes;
    private OctoberChatProviderStatus? _transportFailureStatus;

    public OctoberChatBudget(OctoberChatLimits? limits = null, TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        Limits = (limits ?? new()) with { };
        if (Limits.QuestionCharacters <= 0 || Limits.ProviderRounds <= 0 || Limits.ToolCalls <= 0 ||
            Limits.ConcurrentTools <= 0 || Limits.ToolArgumentBytes <= 0 || Limits.PageSize <= 0 ||
            Limits.ContentBytes <= 0 || Limits.ToolResultBytes <= 0 || Limits.OutputBytes <= 0 ||
            Limits.HistoryTurns <= 0 || Limits.HistoryBytes <= 0 || Limits.Duration <= TimeSpan.Zero ||
            Limits.SseRoundBytes <= 0 || Limits.SseTurnBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "Execution limits must be positive.");
        }

        _clock = timeProvider ?? TimeProvider.System;
        _started = _clock.GetTimestamp();
        _cancellationToken = cancellationToken;
    }

    public OctoberChatLimits Limits { get; }
    public bool IsStopped { get { lock (_gate) return Stopped(); } }
    public int ProviderRounds { get { lock (_gate) return _providerRounds; } }
    public int ToolCalls { get { lock (_gate) return _toolCalls; } }
    public int ActiveTools { get { lock (_gate) return _activeTools; } }
    public long ToolResultBytes { get { lock (_gate) return _toolResultBytes; } }
    public long OutputBytes { get { lock (_gate) return _outputBytes; } }
    public long SseBytes { get { lock (_gate) return _sseBytes; } }
    public CancellationToken OriginalCancellationToken => _cancellationToken;
    internal OctoberChatProviderStatus? TransportFailureStatus { get { lock (_gate) return _transportFailureStatus; } }

    internal void RecordTransportRejection(int observedAttempts, bool budgetExceeded)
    {
        lock (_gate)
        {
            if (_transportFailureStatus is not null) return;
            _transportFailureStatus = budgetExceeded ? OctoberChatProviderStatus.BudgetExceeded : OctoberChatProviderStatus.Failed;
            _toolCalls += Math.Min(Math.Max(0, observedAttempts), Limits.ToolCalls - _toolCalls);
            _stopped = true;
        }
    }

    public CancellationTokenSource CreateDeadlineCancellation()
    {
        lock (_gate)
        {
            TimeSpan remaining = Limits.Duration - _clock.GetElapsedTime(_started);
            return new CancellationTokenSource(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, _clock);
        }
    }

    internal void StopExecution()
    {
        lock (_gate) _stopped = true;
    }

    public bool TryValidateQuestion(string question)
    {
        lock (_gate)
        {
            if (Stopped()) return false;
            if (string.IsNullOrWhiteSpace(question)) return Stop();
            ReadOnlySpan<char> remaining = question.AsSpan();
            int count = 0;
            while (!remaining.IsEmpty)
            {
                if (++count > Limits.QuestionCharacters ||
                    Rune.DecodeFromUtf16(remaining, out _, out int consumed) != OperationStatus.Done)
                {
                    return Stop();
                }

                remaining = remaining[consumed..];
            }

            return true;
        }
    }

    public bool TryStartProviderRound(out int round)
    {
        lock (_gate)
        {
            round = 0;
            if (Stopped()) return false;
            if (_providerRounds >= Limits.ProviderRounds) return Stop();
            round = ++_providerRounds;
            _roundBytes.Add(round, 0);
            return true;
        }
    }

    public bool TryStartToolCall(int argumentBytes, out IDisposable? lease)
    {
        lock (_gate)
        {
            lease = null;
            if (Stopped()) return false;
            if (_toolCalls >= Limits.ToolCalls) return Stop();
            _toolCalls++;
            if (argumentBytes < 0 || argumentBytes > Limits.ToolArgumentBytes) return Stop();
            if (_activeTools >= Limits.ConcurrentTools) return false;
            _activeTools++;
            lease = new ToolLease(this);
            return true;
        }
    }

    public bool TryAddToolResultBytes(long bytes, bool content = false)
    {
        lock (_gate)
        {
            if (Stopped()) return false;
            if (bytes < 0 || bytes > Limits.ToolResultBytes - _toolResultBytes ||
                (content && bytes > Math.Min(Limits.ContentBytes, KnowledgeContract.MaximumContentBytes)))
            {
                return Stop();
            }

            _toolResultBytes += bytes;
            return true;
        }
    }

    public bool TryAddOutput(string text)
    {
        lock (_gate)
        {
            if (Stopped()) return false;
            int bytes;
            try { bytes = _strictUtf8.GetByteCount(text); }
            catch (ArgumentException) { return Stop(); }
            if (bytes > Limits.OutputBytes - _outputBytes) return Stop();
            _outputBytes += bytes;
            return true;
        }
    }

    public bool TryAddSseBytes(int round, int bytes)
    {
        lock (_gate)
        {
            if (Stopped()) return false;
            if (bytes < 0 || !_roundBytes.TryGetValue(round, out long used) ||
                bytes > Limits.SseRoundBytes - used || bytes > Limits.SseTurnBytes - _sseBytes)
            {
                return Stop();
            }

            _roundBytes[round] = used + bytes;
            _sseBytes += bytes;
            return true;
        }
    }

    private bool Stopped()
    {
        _stopped |= _cancellationToken.IsCancellationRequested ||
            _clock.GetElapsedTime(_started) >= Limits.Duration;
        return _stopped;
    }

    private bool Stop()
    {
        _stopped = true;
        return false;
    }

    private sealed class ToolLease(OctoberChatBudget owner) : IDisposable
    {
        private OctoberChatBudget? _owner = owner;
        public void Dispose()
        {
            OctoberChatBudget? current = Interlocked.Exchange(ref _owner, null);
            if (current is null) return;
            lock (current._gate) current._activeTools--;
        }
    }
}
