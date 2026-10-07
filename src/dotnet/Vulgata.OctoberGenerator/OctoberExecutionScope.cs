using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;

namespace Vulgata.OctoberGenerator;

public sealed class OctoberExecutionScope : IOctoberExecutionScope,
    IOctoberWorkerExecutionScope, IAsyncDisposable
{
    private readonly Guid _runId;
    private readonly string _providerProfileId;
    private readonly OctoberScanPolicy _policy;
    private readonly Func<OctoberProviderPhase, CancellationToken, Task<ProviderAdmissionStatus>> _checkEligibility;
    private readonly OperationKey? _finalizationOperation;
    private readonly TimeProvider _time;
    private readonly long _started;
    private readonly TimeSpan _runLifetime;
    private readonly TimeSpan _rateInterval;
    private readonly CancellationTokenSource _runDeadline;
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationTokenRegistration _externalStop;
    private readonly SemaphoreSlim _slots;
    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, AttemptLease> _leases = [];
    private readonly Dictionary<Guid, FileBudget> _files = [];
    private readonly HashSet<Guid> _attemptIds = [];
    private long _remainingTokens;
    private int _admittedRequests;
    private int _pendingAcquisitions;
    private TimeSpan _nextGrant;
    private TimeSpan _nextSend;
    private ProviderAdmissionStatus? _blocked;
    private bool _unknownUsage;
    private bool _disposed;

    public OctoberExecutionScope(Guid runId, string providerProfileId,
        OctoberScanPolicy policy,
        Func<OctoberProviderPhase, CancellationToken, Task<ProviderAdmissionStatus>> checkEligibility,
        OperationKey? finalizationOperation = null, TimeProvider? timeProvider = null,
        CancellationToken hardStop = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(checkEligibility);
        if (runId == Guid.Empty || string.IsNullOrWhiteSpace(providerProfileId) ||
            providerProfileId.Trim() != providerProfileId ||
            !new OctoberScanPolicyValidator().Validate(policy).IsValid)
            throw new ArgumentException("A valid bounded October execution policy is required.");
        _runId = runId;
        _providerProfileId = providerProfileId;
        _policy = policy;
        _checkEligibility = checkEligibility;
        _finalizationOperation = finalizationOperation;
        _time = timeProvider ?? TimeProvider.System;
        _started = _time.GetTimestamp();
        _runLifetime = TimeSpan.FromSeconds(policy.RunDeadlineSeconds);
        _rateInterval = TimeSpan.FromMinutes(1) / policy.MaximumRequestsPerMinute;
        _remainingTokens = policy.MaximumTotalTokens!.Value;
        _slots = new SemaphoreSlim(policy.WorkerCap, policy.WorkerCap);
        _runDeadline = new CancellationTokenSource(_runLifetime, _time);
        _externalStop = hardStop.Register(HardStop);
    }

    public Task<ProviderAdmission> AcquireWorkerAttemptAsync(
        OctoberWorkerProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || request.FileWorkstreamId == Guid.Empty ||
            !ValidRequest(request.RunId, request.AttemptId, request.ProviderProfileId,
                request.MaximumDuration, request.MaximumTokens))
            return Task.FromResult(Denied(ProviderAdmissionStatus.Denied));
        return AcquireAsync(OctoberProviderPhase.Worker, request.FileWorkstreamId,
            request.AttemptId, request.MaximumDuration, request.MaximumTokens, cancellationToken);
    }

    public Task<ProviderAdmission> AcquireProviderAttemptAsync(
        ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || _policy.OutputStage != "cl-and-bl" ||
            _finalizationOperation is null || request.FinalizationOperation != _finalizationOperation ||
            !Enum.IsDefined(request.WorkKind) ||
            !ValidRequest(request.RunId, request.AttemptId, request.ProviderProfileId,
                request.MaximumDuration, request.MaximumTokens))
            return Task.FromResult(Denied(ProviderAdmissionStatus.Denied));
        return AcquireAsync(OctoberProviderPhase.Business, null, request.AttemptId,
            request.MaximumDuration, request.MaximumTokens, cancellationToken);
    }

    public void HardStop()
    {
        Block(ProviderAdmissionStatus.HardStopped);
    }

    public ValueTask DisposeAsync()
    {
        HardStop();
        lock (_sync)
        {
            if (_disposed)
                return ValueTask.CompletedTask;
            if (_leases.Count != 0 || _pendingAcquisitions != 0)
                throw new InvalidOperationException("october_provider_settlement_unconfirmed");
            _disposed = true;
        }
        _externalStop.Dispose();
        _runDeadline.Dispose();
        _stop.Dispose();
        _slots.Dispose();
        return ValueTask.CompletedTask;
    }

    private bool ValidRequest(Guid runId, Guid attemptId, string profile,
        TimeSpan duration, long tokens) => runId == _runId && attemptId != Guid.Empty &&
        profile == _providerProfileId && duration > TimeSpan.Zero && duration <= _runLifetime && tokens > 0;

    private async Task<ProviderAdmission> AcquireAsync(OctoberProviderPhase phase,
        Guid? fileWorkstreamId, Guid attemptId, TimeSpan duration, long tokens,
        CancellationToken cancellationToken)
    {
        TimeSpan effectiveDuration = duration;
        lock (_sync)
        {
            ProviderAdmissionStatus status = CurrentStatus(null, cancellationToken);
            if (status != ProviderAdmissionStatus.Granted)
                return Denied(status);
            if (_admittedRequests >= _policy.MaximumProviderRequests)
                return Denied(ProviderAdmissionStatus.BudgetExhausted);
            if (fileWorkstreamId is { } fileId)
            {
                if (!_files.TryGetValue(fileId, out FileBudget? file))
                {
                    file = new FileBudget(_time.GetTimestamp());
                    _files.Add(fileId, file);
                }
                TimeSpan remaining = TimeSpan.FromSeconds(_policy.FileDeadlineSeconds) -
                    _time.GetElapsedTime(file.Started);
                if (remaining <= TimeSpan.Zero)
                    return Denied(ProviderAdmissionStatus.DeadlineExceeded);
                if (file.AdmittedRequests >= _policy.MaximumModelRequestsPerFile)
                    return Denied(ProviderAdmissionStatus.BudgetExhausted);
                effectiveDuration = remaining < duration ? remaining : duration;
            }
            _pendingAcquisitions++;
        }

        AttemptState? state = null;
        bool slotHeld = false;
        bool granted = false;
        try
        {
            state = new AttemptState(phase, fileWorkstreamId, attemptId, tokens,
                effectiveDuration, _time, _runDeadline.Token, _stop.Token, cancellationToken);
            await _slots.WaitAsync(state.Token);
            slotHeld = true;
            ProviderAdmissionStatus rate = await WaitForRateAsync(state, state.Token);
            if (rate != ProviderAdmissionStatus.Granted)
                return Denied(rate);
            ProviderAdmissionStatus eligibility = await EligibilityAsync(state, state.Token);
            if (eligibility != ProviderAdmissionStatus.Granted)
                return Denied(eligibility);
            lock (_sync)
            {
                ProviderAdmissionStatus status = CurrentStatus(state, cancellationToken);
                if (status != ProviderAdmissionStatus.Granted)
                    return Denied(status);
                if (_attemptIds.Contains(attemptId))
                    return Denied(ProviderAdmissionStatus.Denied);
                if (_admittedRequests >= _policy.MaximumProviderRequests || tokens > _remainingTokens)
                    return Denied(ProviderAdmissionStatus.BudgetExhausted);
                if (fileWorkstreamId is { } fileId &&
                    _files[fileId].AdmittedRequests >= _policy.MaximumModelRequestsPerFile)
                    return Denied(ProviderAdmissionStatus.BudgetExhausted);

                // A grant consumes one finite request reservation, including a later
                // unsent cancellation. Retries never receive a renewed allowance.
                _admittedRequests++;
                if (fileWorkstreamId is { } admittedFileId)
                    _files[admittedFileId].AdmittedRequests++;
                _remainingTokens -= tokens;
                _attemptIds.Add(attemptId);
                var lease = new AttemptLease(this, state);
                _leases.Add(attemptId, lease);
                granted = true;
                slotHeld = false;
                return new ProviderAdmission(ProviderAdmissionStatus.Granted, lease);
            }
        }
        catch (OperationCanceledException)
        {
            return Denied(CancellationStatus(state, cancellationToken));
        }
        finally
        {
            if (slotHeld)
                _slots.Release();
            if (!granted)
                state?.Dispose();
            lock (_sync)
                _pendingAcquisitions--;
        }
    }

    private async Task<ProviderAdmissionStatus> EligibilityAsync(AttemptState state,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ProviderAdmissionStatus status = CurrentStatus(state, cancellationToken);
            if (status != ProviderAdmissionStatus.Granted)
                return status;
        }
        ProviderAdmissionStatus eligibility;
        try
        {
            eligibility = await _checkEligibility(state.Phase, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return CancellationStatus(state, cancellationToken);
        }
        catch (Exception)
        {
            eligibility = ProviderAdmissionStatus.AuthorityUnavailable;
        }
        if (!Enum.IsDefined(eligibility))
            eligibility = ProviderAdmissionStatus.AuthorityUnavailable;
        if (eligibility == ProviderAdmissionStatus.AuthorityUnavailable)
            Block(eligibility);
        lock (_sync)
        {
            ProviderAdmissionStatus current = CurrentStatus(state, cancellationToken);
            return current != ProviderAdmissionStatus.Granted ? current : eligibility;
        }
    }

    private async Task<ProviderAdmissionStatus> WaitForRateAsync(AttemptState state,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan wait;
            lock (_sync)
            {
                ProviderAdmissionStatus status = CurrentStatus(state, cancellationToken);
                if (status != ProviderAdmissionStatus.Granted)
                    return status;
                TimeSpan now = _time.GetElapsedTime(_started);
                wait = _nextGrant - now;
                if (wait <= TimeSpan.Zero)
                {
                    _nextGrant = now + _rateInterval;
                    return ProviderAdmissionStatus.Granted;
                }
            }
            await Task.Delay(wait, _time, cancellationToken);
        }
    }

    // Callers hold _sync; elapsed checks remain authoritative if timer delivery is delayed.
    private ProviderAdmissionStatus CurrentStatus(AttemptState? state, CancellationToken cancellationToken)
    {
        if (_disposed)
            return ProviderAdmissionStatus.HardStopped;
        if (_blocked is { } blocked)
            return blocked;
        if (_time.GetElapsedTime(_started) >= _runLifetime || state?.DeadlineExceeded == true)
            return ProviderAdmissionStatus.DeadlineExceeded;
        if (state?.FileWorkstreamId is { } fileId &&
            _time.GetElapsedTime(_files[fileId].Started) >= TimeSpan.FromSeconds(_policy.FileDeadlineSeconds))
            return ProviderAdmissionStatus.DeadlineExceeded;
        if (cancellationToken.IsCancellationRequested || state?.Token.IsCancellationRequested == true)
            return ProviderAdmissionStatus.Denied;
        return _unknownUsage ? ProviderAdmissionStatus.BudgetExhausted : ProviderAdmissionStatus.Granted;
    }

    private ProviderAdmissionStatus CancellationStatus(AttemptState? state,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ProviderAdmissionStatus status = CurrentStatus(state, cancellationToken);
            if (status != ProviderAdmissionStatus.Granted)
                return status;
        }
        // A dependency's unrelated cancellation is no evidence of live eligibility.
        Block(ProviderAdmissionStatus.AuthorityUnavailable);
        return ProviderAdmissionStatus.AuthorityUnavailable;
    }

    private void Block(ProviderAdmissionStatus reason)
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _blocked ??= reason;
        }
        try { _stop.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private static ProviderAdmission Denied(ProviderAdmissionStatus status) => new(status, null);

    private sealed class FileBudget(long started)
    {
        internal long Started { get; } = started;
        internal int AdmittedRequests { get; set; }
    }

    private sealed class AttemptState : IDisposable
    {
        private readonly TimeProvider _time;
        private readonly long _started;
        private readonly TimeSpan _duration;
        private readonly CancellationTokenSource _deadline;
        private readonly CancellationTokenSource _linked;

        internal AttemptState(OctoberProviderPhase phase, Guid? fileWorkstreamId,
            Guid attemptId, long tokens, TimeSpan duration, TimeProvider time,
            CancellationToken run, CancellationToken stop, CancellationToken caller)
        {
            Phase = phase;
            FileWorkstreamId = fileWorkstreamId;
            AttemptId = attemptId;
            Tokens = tokens;
            _time = time;
            _started = time.GetTimestamp();
            _duration = duration;
            _deadline = new CancellationTokenSource(duration, time);
            _linked = CancellationTokenSource.CreateLinkedTokenSource(run, stop, caller, _deadline.Token);
            Token = _linked.Token;
        }

        internal OctoberProviderPhase Phase { get; }
        internal Guid? FileWorkstreamId { get; }
        internal Guid AttemptId { get; }
        internal long Tokens { get; }
        internal CancellationToken Token { get; }
        internal bool DeadlineExceeded => _time.GetElapsedTime(_started) >= _duration;
        public void Dispose() { _linked.Dispose(); _deadline.Dispose(); }
    }

    private sealed class AttemptLease(OctoberExecutionScope owner, AttemptState state)
        : IOctoberProviderAttemptLease
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private bool _sent;
        private bool _released;
        private ProviderAttemptUsage? _settled;
        private Exception? _disposalFailure;

        public Guid AttemptId => state.AttemptId;
        public CancellationToken CancellationToken => state.Token;

        public async Task<ProviderAdmissionStatus> CheckBeforeSendAsync(
            CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(state.Token, cancellationToken);
            bool gateHeld = false;
            try
            {
                await _sendGate.WaitAsync(linked.Token);
                gateHeld = true;
                while (true)
                {
                    ProviderAdmissionStatus eligible = await owner.EligibilityAsync(state, linked.Token);
                    if (eligible != ProviderAdmissionStatus.Granted)
                        return eligible;
                    TimeSpan wait;
                    lock (owner._sync)
                    {
                        ProviderAdmissionStatus current = owner.CurrentStatus(state, linked.Token);
                        if (current != ProviderAdmissionStatus.Granted)
                            return current;
                        if (_released)
                            return ProviderAdmissionStatus.Denied;
                        if (_sent)
                            return ProviderAdmissionStatus.Granted;
                        TimeSpan now = owner._time.GetElapsedTime(owner._started);
                        wait = owner._nextSend - now;
                        if (wait <= TimeSpan.Zero)
                        {
                            // Reserve rate only after the last awaited authority check.
                            // A delayed check cannot revive an already-matured send slot.
                            owner._nextSend = now + owner._rateInterval;
                            _sent = true;
                            return ProviderAdmissionStatus.Granted;
                        }
                    }
                    await Task.Delay(wait, owner._time, linked.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return owner.CancellationStatus(state, linked.Token);
            }
            finally
            {
                if (gateHeld) _sendGate.Release();
            }
        }

        public Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(usage);
            bool conflict = false;
            lock (owner._sync)
            {
                if (_settled is not null)
                {
                    if (_settled == usage) return Task.CompletedTask;
                    conflict = true;
                }
                else
                {
                    if (_released)
                        throw new InvalidOperationException("october_provider_lease_released");
                    bool known = usage.InputTokens is >= 0 && usage.OutputTokens is >= 0 &&
                        usage.InputTokens.Value <= long.MaxValue - usage.OutputTokens.Value &&
                        usage.Cost is not < 0 && Enum.IsDefined(usage.Outcome) &&
                        usage.WaitDuration >= TimeSpan.Zero && usage.ExecutionDuration >= TimeSpan.Zero;
                    if (known)
                    {
                        long actual = usage.InputTokens!.Value + usage.OutputTokens!.Value;
                        if (actual <= state.Tokens)
                            owner._remainingTokens += state.Tokens - actual;
                        else
                        {
                            owner._remainingTokens = Math.Max(0, owner._remainingTokens - (actual - state.Tokens));
                            owner._unknownUsage = true;
                        }
                    }
                    else
                    {
                        // Keep the full conservative reservation; never charge unknown as zero.
                        owner._unknownUsage = true;
                    }
                    _settled = usage;
                    ReleaseLocked();
                }
            }
            if (conflict)
            {
                owner.Block(ProviderAdmissionStatus.HardStopped);
                throw new InvalidOperationException("october_provider_usage_conflict");
            }
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            lock (owner._sync)
            {
                if (_disposalFailure is not null)
                    throw _disposalFailure;
                if (_released)
                    return ValueTask.CompletedTask;
                if (!_sent)
                {
                    owner._remainingTokens += state.Tokens;
                    ReleaseLocked();
                    return ValueTask.CompletedTask;
                }
                _disposalFailure = new InvalidOperationException("october_provider_transport_unsettled");
            }
            owner.Block(ProviderAdmissionStatus.HardStopped);
            throw _disposalFailure;
        }

        private void ReleaseLocked()
        {
            _released = true;
            owner._leases.Remove(AttemptId);
            owner._slots.Release();
            state.Dispose();
        }
    }
}
