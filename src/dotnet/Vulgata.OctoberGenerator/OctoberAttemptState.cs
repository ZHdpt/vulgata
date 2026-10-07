using System.Text;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>Owns one Worker invocation and its exact process-local acceptance attempt.</summary>
public sealed class OctoberAttemptState : IOctoberFileAcceptanceAuthority,
    IOctoberCommitSubmissionGuard, IAsyncDisposable
{
    private readonly OperationKey _operation;
    private readonly OctoberFileWorkerRequest _request;
    private readonly PreparedOrigin _origin;
    private readonly string _fileIdentity;
    private readonly long _byteLength;
    private readonly int _lineCount;
    private readonly Func<CancellationToken, Task<OctoberFileWorkerResult>> _executeWorker;
    private readonly Func<CancellationToken, Task<DisclosureStatus>> _checkAuthority;
    private readonly TimeProvider _time;
    private readonly long _started;
    private readonly TimeSpan _lifetime;
    private readonly CancellationToken _hardStop;
    private readonly CancellationTokenSource _deadline;
    private readonly CancellationTokenSource _preSubmissionStop = new();
    private readonly SemaphoreSlim _submissionGate = new(1, 1);
    private readonly Lock _sync = new();
    private readonly HashSet<Task> _outstanding = [];
    private Task<CompletedFileSubmission?>? _execution;
    private Task<KnowledgeWriteResult>? _acceptance;
    private Task<CommitSettlement>? _commit;
    private CompletedFileSubmission? _submission;
    private OctoberFileWorkerResult? _workerResult;
    private CommitSettlement? _settlement;
    private bool _hasSubmitted;
    private bool _submissionUncertain;
    private bool _fenced;
    private bool _disposed;
    private int _activeCalls;

    public OctoberAttemptState(OperationKey operation, OctoberFileWorkerRequest request,
        Func<CancellationToken, Task<OctoberFileWorkerResult>> executeWorker,
        Func<CancellationToken, Task<DisclosureStatus>> checkAuthority,
        TimeSpan maximumLifetime, TimeProvider? timeProvider = null,
        CancellationToken hardStop = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(executeWorker);
        ArgumentNullException.ThrowIfNull(checkAuthority);
        if (operation.Kind != OperationKind.AcceptFile || operation.OperationId == Guid.Empty ||
            request.Binding is null || operation.BatchId != request.Binding.BatchId ||
            request.AdmissionId == Guid.Empty || request.AttemptId == Guid.Empty || request.Source is null ||
            maximumLifetime <= TimeSpan.Zero || maximumLifetime > TimeSpan.FromMinutes(30))
            throw new ArgumentException("An exact bounded file attempt is required.");
        _operation = operation;
        _request = request;
        _origin = request.Source.Origin;
        _fileIdentity = request.Source.FileIdentity;
        _byteLength = request.Source.ByteLength;
        _lineCount = request.Source.LineCount;
        _executeWorker = executeWorker;
        _checkAuthority = checkAuthority;
        _time = timeProvider ?? TimeProvider.System;
        _started = _time.GetTimestamp();
        _lifetime = maximumLifetime;
        _hardStop = hardStop;
        _deadline = new CancellationTokenSource(maximumLifetime, _time);
    }

    public CompletedFileSubmission? Submission { get { lock (_sync) return _submission; } }
    public OctoberFileWorkerResult? WorkerResult { get { lock (_sync) return _workerResult; } }
    public CommitSettlement? Settlement { get { lock (_sync) return _settlement; } }
    public bool HasSubmitted { get { lock (_sync) return _hasSubmitted; } }

    /// <summary>Settlement precondition only; does not grant corpus or publication authority.</summary>
    public bool IsSettledForFreeze
    {
        get
        {
            lock (_sync)
                return _activeCalls == 0 && !_submissionUncertain &&
                    _commit is not { IsCompleted: false } && !_outstanding.Any(task => !task.IsCompleted) &&
                    _settlement is (CommitSettlement.Committed or CommitSettlement.ConfirmedNotCommitted);
        }
    }

    public Task<CompletedFileSubmission?> ExecuteAndSealAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<CompletedFileSubmission?> completion;
        lock (_sync)
        {
            if (_execution is not null) return _execution;
            if (_disposed) return Task.FromResult<CompletedFileSubmission?>(null);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _execution = completion.Task;
            _activeCalls++;
        }
        _ = ExecuteCoreAsync(completion, cancellationToken);
        return completion.Task;
    }

    public async Task<KnowledgeWriteResult> AcceptAsync(IOctoberKnowledgeWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        TaskCompletionSource<KnowledgeWriteResult>? completion = null;
        Task<KnowledgeWriteResult> acceptance;
        lock (_sync)
        {
            if (_disposed) return Result(KnowledgeWriteStatus.StaleOrFenced);
            if (_acceptance is null)
            {
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _acceptance = completion.Task;
            }
            acceptance = _acceptance;
        }
        if (completion is not null)
            _ = AcceptCoreAsync(writer, completion, cancellationToken);
        try
        {
            KnowledgeWriteResult result = await acceptance.WaitAsync(cancellationToken);
            return await FilterDisclosureAsync(result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result(KnowledgeWriteStatus.CommitIndeterminate);
        }
    }

    public async Task<KnowledgeWriteResult> ReconcileAsync(IOctoberKnowledgeWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        CompletedFileSubmission? submission = Submission;
        if (submission is null || !Enter()) return Result(KnowledgeWriteStatus.NotCompleted);
        try
        {
            // Reconciliation is read-only settlement of an existing operation, not
            // a new analysis/write allowance after the original deadline or fence.
            using var lookupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lookupDeadline.Token);
            DisclosureStatus authority = await CheckAuthorityAsync(linked.Token);
            if (authority != DisclosureStatus.Allowed) return Result(AuthorityStatus(authority));
            Task<KnowledgeWriteResult> lookup = writer.LookupOperationAsync(_request.Binding,
                _operation, submission.Proposal.CanonicalProposalDigest, linked.Token);
            KnowledgeWriteResult result = await BoundedAsync(lookup, linked.Token);
            RecordOutcome(result, definitiveWriteResult: false);
            return await FilterDisclosureAsync(result, linked.Token);
        }
        catch (Exception)
        {
            return Result(KnowledgeWriteStatus.CommitIndeterminate);
        }
        finally { Exit(); }
    }

    public async Task<DisclosureStatus> VerifyAsync(CompletedFileSubmission submission,
        CancellationToken cancellationToken = default)
    {
        if (!MatchesSubmission(submission) || !Enter()) return DisclosureStatus.Denied;
        try
        {
            using var linked = BeforeSubmissionToken(cancellationToken);
            return await LiveAsync(linked.Token);
        }
        catch (OperationCanceledException) { return DisclosureStatus.Denied; }
        catch (Exception) { return DisclosureStatus.AuthorityUnavailable; }
        finally { Exit(); }
    }

    public async Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        if (!Enter()) return CommitEligibilityStatus.StaleOrFenced;
        try
        {
            CommitEligibilityStatus current = Eligibility(attempt, cancellationToken);
            if (current != CommitEligibilityStatus.Eligible) return current;
            using var linked = BeforeSubmissionToken(cancellationToken);
            DisclosureStatus live = await LiveAsync(linked.Token);
            current = Eligibility(attempt, cancellationToken);
            return current != CommitEligibilityStatus.Eligible ? current : ToEligibility(live);
        }
        catch (OperationCanceledException) { return CancellationEligibility(attempt, cancellationToken); }
        catch (Exception) { return CommitEligibilityStatus.AuthorityUnavailable; }
        finally { Exit(); }
    }

    public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt,
        Func<CancellationToken, SubmittedCommit> submitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submitAsync);
        if (!Enter()) return GuardDenied(CommitEligibilityStatus.StaleOrFenced);
        bool gateHeld = false;
        CancellationTokenSource? commitToken = null;
        Task<CommitSettlement>? actual = null;
        try
        {
            CommitEligibilityStatus current = Eligibility(attempt, cancellationToken);
            if (current != CommitEligibilityStatus.Eligible) return GuardDenied(current);
            using var before = BeforeSubmissionToken(cancellationToken);
            await _submissionGate.WaitAsync(before.Token);
            gateHeld = true;
            current = Eligibility(attempt, cancellationToken);
            if (current != CommitEligibilityStatus.Eligible) return GuardDenied(current);
            DisclosureStatus live = await LiveAsync(before.Token);
            if (live != DisclosureStatus.Allowed) return GuardDenied(ToEligibility(live));

            // This token intentionally excludes a later eligibility fence: once
            // invoked, the original commit must settle rather than be called rolled back.
            commitToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _deadline.Token, _hardStop);
            lock (_sync)
            {
                current = Eligibility(attempt, cancellationToken);
                if (current != CommitEligibilityStatus.Eligible) return GuardDenied(current);
                if (_commit is not null)
                {
                    actual = _commit;
                }
                else if (_submissionUncertain)
                {
                    return new(CommitSubmissionStatus.StaleOrFenced, CommitSettlement.Indeterminate);
                }
                else
                {
                    _submissionUncertain = true;
                    _settlement = CommitSettlement.Indeterminate;
                    SubmittedCommit submitted = submitAsync(commitToken.Token);
                    if (submitted?.Settlement is null)
                        return new(CommitSubmissionStatus.StaleOrFenced, CommitSettlement.Indeterminate);
                    _hasSubmitted = true;
                    _submissionUncertain = false;
                    actual = ObserveCommitAsync(submitted.Settlement, commitToken);
                    _commit = actual;
                    commitToken = null; // The settlement observer owns this token until completion.
                }
            }
            _submissionGate.Release();
            gateHeld = false;
            try
            {
                using var remaining = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _deadline.Token, _hardStop);
                CommitSettlement settlement = await actual.WaitAsync(remaining.Token);
                return new(CommitSubmissionStatus.Submitted, settlement);
            }
            catch (OperationCanceledException)
            {
                return new(CommitSubmissionStatus.Submitted, CommitSettlement.Indeterminate);
            }
        }
        catch (OperationCanceledException)
        {
            return GuardDenied(CancellationEligibility(attempt, cancellationToken));
        }
        catch (Exception)
        {
            lock (_sync)
                if (_submissionUncertain || _hasSubmitted) _settlement ??= CommitSettlement.Indeterminate;
            return new(_hasSubmitted ? CommitSubmissionStatus.Submitted : CommitSubmissionStatus.AuthorityUnavailable,
                _submissionUncertain || _hasSubmitted ? CommitSettlement.Indeterminate : null);
        }
        finally
        {
            commitToken?.Dispose();
            if (gateHeld) _submissionGate.Release();
            Exit();
        }
    }

    public async Task FenceAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync) if (_disposed) return;
        _preSubmissionStop.Cancel();
        await _submissionGate.WaitAsync(cancellationToken);
        try { lock (_sync) _fenced = true; }
        finally { _submissionGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await FenceAsync();
        lock (_sync)
        {
            if (_disposed) return;
            if (_activeCalls != 0 || _outstanding.Any(task => !task.IsCompleted) ||
                _commit is { IsCompleted: false } || _submissionUncertain ||
                _settlement == CommitSettlement.Indeterminate)
                throw new InvalidOperationException("october_attempt_settlement_unconfirmed");
            _disposed = true;
        }
        _deadline.Dispose();
        _preSubmissionStop.Dispose();
        _submissionGate.Dispose();
    }

    private async Task ExecuteCoreAsync(TaskCompletionSource<CompletedFileSubmission?> completion,
        CancellationToken cancellationToken)
    {
        CompletedFileSubmission? sealedSubmission = null;
        try
        {
            using var linked = BeforeSubmissionToken(cancellationToken);
            if (Eligibility(null, cancellationToken, requireSeal: false) != CommitEligibilityStatus.Eligible ||
                await LiveAsync(linked.Token) != DisclosureStatus.Allowed)
                return;
            OctoberFileWorkerResult result = await BoundedAsync(_executeWorker(linked.Token), linked.Token);
            lock (_sync) _workerResult = result;
            CompletedFileProposal? proposal = result.Proposal;
            if (result.Status != OctoberFileWorkerStatus.Completed || proposal is null ||
                result.CoveredUtf8Bytes != _byteLength || result.ProviderRounds <= 0 ||
                result.ProviderRounds > _request.Limits.MaximumProviderRounds ||
                result.SettledToolCalls < 0 || result.SettledToolCalls > _request.Limits.MaximumToolCalls ||
                proposal.Binding != _request.Binding || proposal.AdmissionId != _request.AdmissionId ||
                proposal.AttemptId != _request.AttemptId || proposal.Origin != _origin ||
                proposal.Contributors.IsDefault || _request.Contributors.IsDefault ||
                proposal.Contributors.Length != _request.Contributors.Length ||
                !proposal.Contributors.ToHashSet().SetEquals(_request.Contributors) ||
                !OctoberKnowledgePolicy.Validate(proposal, new(_origin, _lineCount)) ||
                OctoberKnowledgePolicy.ComputeProposalDigest(proposal) != proposal.CanonicalProposalDigest ||
                await LiveAsync(linked.Token) != DisclosureStatus.Allowed)
                return;
            lock (_sync)
            {
                if (Eligibility(null, cancellationToken, requireSeal: false) != CommitEligibilityStatus.Eligible)
                    return;
                sealedSubmission = new CompletedFileSubmission(_operation, proposal, new CompletionProof(proposal));
                _submission = sealedSubmission;
            }
        }
        catch (OperationCanceledException)
        {
            lock (_sync) _workerResult ??= new(OctoberFileWorkerStatus.Cancelled);
        }
        catch (Exception)
        {
            lock (_sync) _workerResult ??= new(OctoberFileWorkerStatus.InvalidOutput);
        }
        finally
        {
            lock (_sync)
                if (sealedSubmission is null && !_hasSubmitted && !_submissionUncertain)
                    _settlement = CommitSettlement.ConfirmedNotCommitted;
            Exit();
            completion.TrySetResult(sealedSubmission);
        }
    }

    private async Task AcceptCoreAsync(IOctoberKnowledgeWriter writer,
        TaskCompletionSource<KnowledgeWriteResult> completion, CancellationToken cancellationToken)
    {
        if (!Enter()) { completion.TrySetResult(Result(KnowledgeWriteStatus.StaleOrFenced)); return; }
        KnowledgeWriteResult result = Result(KnowledgeWriteStatus.NotCompleted);
        try
        {
            CompletedFileSubmission? submission = await ExecuteAndSealAsync(cancellationToken);
            if (submission is null) return;
            CommitEligibilityStatus current = Eligibility(new(_operation, _request.AttemptId,
                submission.Proposal.CanonicalProposalDigest), cancellationToken);
            if (current != CommitEligibilityStatus.Eligible)
            {
                result = Result(WriteStatus(current));
                RecordOutcome(result, definitiveWriteResult: true);
                return;
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _deadline.Token, _hardStop);
            Task<KnowledgeWriteResult> write = ObserveWriteAsync(writer.AcceptCompletedFileAsync(submission, linked.Token));
            result = await BoundedAsync(write, linked.Token);
        }
        catch (Exception)
        {
            result = Result(KnowledgeWriteStatus.CommitIndeterminate);
            lock (_sync) _settlement ??= CommitSettlement.Indeterminate;
        }
        finally { Exit(); completion.TrySetResult(result); }
    }

    private async Task<KnowledgeWriteResult> ObserveWriteAsync(Task<KnowledgeWriteResult> write)
    {
        KnowledgeWriteResult result = await write;
        RecordOutcome(result, definitiveWriteResult: true);
        return result;
    }

    private async Task<CommitSettlement> ObserveCommitAsync(Task<CommitSettlement> commit,
        CancellationTokenSource cancellation)
    {
        try
        {
            CommitSettlement result;
            try { result = await commit; }
            catch (Exception) { result = CommitSettlement.Indeterminate; }
            if (!Enum.IsDefined(result)) result = CommitSettlement.Indeterminate;
            lock (_sync)
                if (_settlement != CommitSettlement.Committed) _settlement = result;
            return result;
        }
        finally { cancellation.Dispose(); }
    }

    private void RecordOutcome(KnowledgeWriteResult result, bool definitiveWriteResult)
    {
        lock (_sync)
        {
            if (result.Operation != _operation) { _submissionUncertain = true; return; }
            if (result.Status is KnowledgeWriteStatus.Denied or KnowledgeWriteStatus.AuthorityUnavailable)
                _fenced = true;
            if (result.Status is (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable) &&
                result.AcceptedFile is { } accepted && _submission is { } submission &&
                accepted.AdmissionId == _request.AdmissionId &&
                accepted.ProposalDigest == submission.Proposal.CanonicalProposalDigest &&
                accepted.Coverage.Origin == _origin)
            {
                _settlement = CommitSettlement.Committed;
                _submissionUncertain = false;
            }
            else if (_settlement != CommitSettlement.Committed && definitiveWriteResult && !_hasSubmitted && !_submissionUncertain &&
                result.Status is (KnowledgeWriteStatus.Denied or KnowledgeWriteStatus.AuthorityUnavailable or
                    KnowledgeWriteStatus.InvalidProposal or KnowledgeWriteStatus.NotCompleted or
                    KnowledgeWriteStatus.StaleOrFenced or KnowledgeWriteStatus.CancelledBeforeCommit or
                    KnowledgeWriteStatus.StorageUnavailable or KnowledgeWriteStatus.Conflict))
            {
                _settlement = CommitSettlement.ConfirmedNotCommitted;
                _submissionUncertain = false;
            }
        }
    }

    private async Task<KnowledgeWriteResult> FilterDisclosureAsync(KnowledgeWriteResult result,
        CancellationToken cancellationToken)
    {
        if (result.Operation != _operation) return Result(KnowledgeWriteStatus.CommitIndeterminate);
        if (result.AcceptedFile is null && result.Corpus is null && result.Publication is null) return result;
        if (!Enter()) return Result(KnowledgeWriteStatus.Denied);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            DisclosureStatus authority = await CheckAuthorityAsync(linked.Token);
            return authority == DisclosureStatus.Allowed ? result : Result(AuthorityStatus(authority));
        }
        catch (Exception) { return Result(KnowledgeWriteStatus.AuthorityUnavailable); }
        finally { Exit(); }
    }

    private async Task<DisclosureStatus> LiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_request.Source.Origin != _origin || _request.Source.FileIdentity != _fileIdentity ||
                _request.Source.ByteLength != _byteLength || _request.Source.LineCount != _lineCount ||
                _byteLength < 0 || _byteLength > int.MaxValue || _lineCount < 0 ||
                string.IsNullOrWhiteSpace(_fileIdentity) ||
                new UTF8Encoding(false, true).GetByteCount(_request.Source.Text) != _byteLength ||
                !string.Equals(OctoberKnowledgePolicy.Hash(_request.Source.Text), _origin.SourceDigest, StringComparison.OrdinalIgnoreCase) ||
                !await BoundedAsync(_request.Source.VerifyCurrentAsync(cancellationToken), cancellationToken))
                return ObserveAuthority(DisclosureStatus.Denied);
            return await CheckAuthorityAsync(cancellationToken);
        }
        catch
        {
            ObserveAuthority(DisclosureStatus.AuthorityUnavailable);
            throw;
        }
    }

    private async Task<DisclosureStatus> CheckAuthorityAsync(CancellationToken cancellationToken)
    {
        try
        {
            DisclosureStatus authority = await BoundedAsync(_checkAuthority(cancellationToken), cancellationToken);
            return ObserveAuthority(Enum.IsDefined(authority) ? authority : DisclosureStatus.AuthorityUnavailable);
        }
        catch
        {
            ObserveAuthority(DisclosureStatus.AuthorityUnavailable);
            throw;
        }
    }

    private DisclosureStatus ObserveAuthority(DisclosureStatus authority)
    {
        // Live checks can run while holding the submission gate. The same lock
        // used by the final submit check makes loss irreversible without waiting
        // on that gate or cancelling an already-submitted commit.
        if (authority != DisclosureStatus.Allowed)
            lock (_sync) _fenced = true;
        return authority;
    }

    private async Task<T> BoundedAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        lock (_sync) _outstanding.Add(task);
        try { return await task.WaitAsync(cancellationToken); }
        finally
        {
            if (task.IsCompleted) Observe(task);
            else _ = task.ContinueWith(Observe, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private void Observe(Task task)
    {
        if (task.IsFaulted) _ = task.Exception;
        lock (_sync) _outstanding.Remove(task);
    }

    private bool MatchesSubmission(CompletedFileSubmission? submission)
    {
        lock (_sync)
        {
            if (submission is null || _submission is null || submission.Operation != _operation ||
                !ReferenceEquals(submission.Proof, _submission.Proof) ||
                submission.Proposal.CanonicalProposalDigest != _submission.Proposal.CanonicalProposalDigest)
                return false;
            try { return OctoberKnowledgePolicy.ComputeProposalDigest(submission.Proposal) == _submission.Proposal.CanonicalProposalDigest; }
            catch (Exception) { return false; }
        }
    }

    private CommitEligibilityStatus Eligibility(CommitAttempt? attempt, CancellationToken cancellationToken,
        bool requireSeal = true)
    {
        lock (_sync)
        {
            if (_disposed || _fenced || _preSubmissionStop.IsCancellationRequested || _hardStop.IsCancellationRequested ||
                _time.GetElapsedTime(_started) >= _lifetime) return CommitEligibilityStatus.StaleOrFenced;
            if (cancellationToken.IsCancellationRequested) return CommitEligibilityStatus.Cancelled;
            if (requireSeal && (_submission is null || attempt is null || attempt.Operation != _operation ||
                attempt.AttemptId != _request.AttemptId || attempt.PayloadDigest != _submission.Proposal.CanonicalProposalDigest))
                return CommitEligibilityStatus.Denied;
            return CommitEligibilityStatus.Eligible;
        }
    }

    private CommitEligibilityStatus CancellationEligibility(CommitAttempt attempt, CancellationToken token)
    {
        CommitEligibilityStatus status = Eligibility(attempt, token);
        return status == CommitEligibilityStatus.Eligible ? CommitEligibilityStatus.AuthorityUnavailable : status;
    }

    private CancellationTokenSource BeforeSubmissionToken(CancellationToken caller) =>
        CancellationTokenSource.CreateLinkedTokenSource(caller, _deadline.Token, _hardStop, _preSubmissionStop.Token);

    private bool Enter() { lock (_sync) { if (_disposed) return false; _activeCalls++; return true; } }
    private void Exit() { lock (_sync) _activeCalls--; }
    private KnowledgeWriteResult Result(KnowledgeWriteStatus status) => new(status, _operation);
    private static KnowledgeWriteStatus AuthorityStatus(DisclosureStatus status) => status == DisclosureStatus.Denied
        ? KnowledgeWriteStatus.Denied : KnowledgeWriteStatus.AuthorityUnavailable;
    private static CommitEligibilityStatus ToEligibility(DisclosureStatus status) => status switch
    {
        DisclosureStatus.Allowed => CommitEligibilityStatus.Eligible,
        DisclosureStatus.Denied => CommitEligibilityStatus.Denied,
        _ => CommitEligibilityStatus.AuthorityUnavailable
    };
    private static KnowledgeWriteStatus WriteStatus(CommitEligibilityStatus status) => status switch
    {
        CommitEligibilityStatus.Denied => KnowledgeWriteStatus.Denied,
        CommitEligibilityStatus.AuthorityUnavailable => KnowledgeWriteStatus.AuthorityUnavailable,
        CommitEligibilityStatus.Cancelled => KnowledgeWriteStatus.CancelledBeforeCommit,
        _ => KnowledgeWriteStatus.StaleOrFenced
    };
    private static CommitGuardResult GuardDenied(CommitEligibilityStatus status) => new(status switch
    {
        CommitEligibilityStatus.Denied => CommitSubmissionStatus.Denied,
        CommitEligibilityStatus.AuthorityUnavailable => CommitSubmissionStatus.AuthorityUnavailable,
        CommitEligibilityStatus.Cancelled => CommitSubmissionStatus.CancelledBeforeSubmission,
        _ => CommitSubmissionStatus.StaleOrFenced
    }, null);

    private sealed class CompletionProof(CompletedFileProposal proposal)
        : CompletedFileProof(proposal.AdmissionId, proposal.AttemptId, proposal.CanonicalProposalDigest);
}
