using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>Trusted closed inventory input, not authority to begin or resume Generate.</summary>
internal sealed record OctoberClPublicationRequest(
    KnowledgeBinding Binding,
    OperationKey FreezeOperation,
    OperationKey PublicationOperation,
    ImmutableArray<AcceptedFile> AcceptedFiles,
    ImmutableArray<FileCoverage> TerminalCoverage,
    ImmutableArray<Uncertainty> Uncertainties,
    bool SelectedScopeComplete,
    string RequestedStage);

/// <summary>The B0-only publication component used by Generate after file attempts have settled.</summary>
internal sealed class OctoberClPublicationPhase : IOctoberCommitSubmissionGuard, IAsyncDisposable
{
    private readonly OctoberClPublicationRequest _request;
    private readonly Func<IOctoberCommitSubmissionGuard, IOctoberCorpusFreezeAuthority, IOctoberKnowledgeWriter> _createWriter;
    private readonly IOctoberCorpusFreezeAuthority _freezeAuthority;
    private readonly Func<CancellationToken, Task<DisclosureStatus>> _checkAuthority;
    private readonly TimeProvider _time;
    private readonly long _started;
    private readonly TimeSpan _lifetime;
    private readonly CancellationToken _hardStop;
    private readonly CancellationTokenSource _deadline;
    private readonly CancellationTokenSource _beforeStop = new();
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private readonly SemaphoreSlim _submissionGate = new(1, 1);
    private readonly Lock _sync = new();
    private readonly HashSet<Task> _outstanding = [];
    private readonly CorpusFreezeRequest _freezeRequest;
    private readonly PhaseWrite _freeze;
    private PhaseWrite? _publication;
    private PublicationRequest? _publicationRequest;
    private IOctoberKnowledgeWriter? _writer;
    private TimeSpan _storeWait;
    private bool _executionStarted;
    private bool _fenced;
    private bool _disposed;
    private int _activeCalls;

    internal OctoberClPublicationPhase(OctoberClPublicationRequest request,
        Func<IOctoberCommitSubmissionGuard, IOctoberCorpusFreezeAuthority, IOctoberKnowledgeWriter> createWriter,
        IOctoberCorpusFreezeAuthority freezeAuthority,
        Func<CancellationToken, Task<DisclosureStatus>> checkAuthority,
        TimeSpan remainingLifetime,
        TimeProvider? timeProvider = null,
        CancellationToken hardStop = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(createWriter);
        ArgumentNullException.ThrowIfNull(freezeAuthority);
        ArgumentNullException.ThrowIfNull(checkAuthority);
        if (request.Binding is null || request.FreezeOperation is null || request.PublicationOperation is null ||
            request.FreezeOperation.BatchId != request.Binding.BatchId ||
            request.PublicationOperation.BatchId != request.Binding.BatchId ||
            request.FreezeOperation.Kind != OperationKind.FreezeCorpus ||
            request.PublicationOperation.Kind != OperationKind.PublishSelection ||
            request.FreezeOperation.OperationId == Guid.Empty || request.PublicationOperation.OperationId == Guid.Empty ||
            request.AcceptedFiles.IsDefault || request.TerminalCoverage.IsDefault || request.Uncertainties.IsDefault ||
            remainingLifetime <= TimeSpan.Zero || remainingLifetime > TimeSpan.FromMinutes(30))
            throw new ArgumentException("An exact closed publication request and original remaining lifetime are required.");
        _request = request;
        _createWriter = createWriter;
        _freezeAuthority = freezeAuthority;
        _checkAuthority = checkAuthority;
        _time = timeProvider ?? TimeProvider.System;
        _started = _time.GetTimestamp();
        _lifetime = remainingLifetime;
        _hardStop = hardStop;
        _deadline = new(remainingLifetime, _time);
        var freeze = new CorpusFreezeRequest(request.FreezeOperation, request.Binding, "",
            request.AcceptedFiles, request.TerminalCoverage);
        _freezeRequest = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        _freeze = new(_freezeRequest.Operation, _freezeRequest.CanonicalPayloadDigest);
    }

    internal Task<OctoberScanReport> ExecuteAsync(CancellationToken cancellationToken = default) =>
        RunAsync(reconcile: false, cancellationToken);

    internal Task<OctoberScanReport> ReconcileAsync(CancellationToken cancellationToken = default) =>
        RunAsync(reconcile: true, cancellationToken);

    internal async Task FenceAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync) if (_disposed) return;
        _beforeStop.Cancel();
        await _submissionGate.WaitAsync(cancellationToken);
        try { lock (_sync) _fenced = true; }
        finally { _submissionGate.Release(); }
    }

    public async Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        if (!Enter()) return CommitEligibilityStatus.StaleOrFenced;
        try
        {
            var current = Eligibility(attempt, cancellationToken);
            if (current != CommitEligibilityStatus.Eligible) return current;
            using var before = BeforeToken(cancellationToken);
            var live = await LiveAsync(before.Token);
            current = Eligibility(attempt, cancellationToken);
            return current == CommitEligibilityStatus.Eligible ? live : current;
        }
        catch (OperationCanceledException) { return CancelStatus(attempt, cancellationToken); }
        catch (Exception) { LoseAuthority(); return CommitEligibilityStatus.AuthorityUnavailable; }
        finally { Exit(); }
    }

    public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt,
        Func<CancellationToken, SubmittedCommit> submitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submitAsync);
        if (!Enter()) return GuardFailure(CommitEligibilityStatus.StaleOrFenced);
        bool held = false;
        CancellationTokenSource? commitToken = null;
        PhaseWrite? write = null;
        try
        {
            using var before = BeforeToken(cancellationToken);
            await _submissionGate.WaitAsync(before.Token);
            held = true;
            var current = Eligibility(attempt, cancellationToken);
            if (current != CommitEligibilityStatus.Eligible) return GuardFailure(current);
            var live = await LiveAsync(before.Token);
            if (live != CommitEligibilityStatus.Eligible) return GuardFailure(live);
            commitToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _hardStop, _deadline.Token);
            Task<CommitSettlement> actual;
            lock (_sync)
            {
                current = Eligibility(attempt, cancellationToken);
                if (current != CommitEligibilityStatus.Eligible) return GuardFailure(current);
                write = Find(attempt)!;
                if (!write.Started) return GuardFailure(CommitEligibilityStatus.Denied);
                if (write.Commit is not null) actual = write.Commit;
                else if (write.Uncertain) return new(CommitSubmissionStatus.StaleOrFenced, CommitSettlement.Indeterminate);
                else
                {
                    write.Uncertain = true;
                    write.Settlement = CommitSettlement.Indeterminate;
                    // No await between final eligibility and the actual provider commit call.
                    SubmittedCommit submitted = submitAsync(commitToken.Token);
                    if (submitted?.Settlement is null)
                        return new(CommitSubmissionStatus.AuthorityUnavailable, CommitSettlement.Indeterminate);
                    write.Submitted = true;
                    write.Uncertain = false;
                    actual = ObserveCommitAsync(write, submitted.Settlement, commitToken);
                    write.Commit = actual;
                    commitToken = null;
                }
            }
            _submissionGate.Release();
            held = false;
            using var remaining = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _hardStop, _deadline.Token);
            try { return new(CommitSubmissionStatus.Submitted, await actual.WaitAsync(remaining.Token)); }
            catch (OperationCanceledException) { return new(CommitSubmissionStatus.Submitted, CommitSettlement.Indeterminate); }
        }
        catch (OperationCanceledException) { return GuardFailure(CancelStatus(attempt, cancellationToken)); }
        catch (Exception)
        {
            return new(write?.Submitted == true ? CommitSubmissionStatus.Submitted : CommitSubmissionStatus.AuthorityUnavailable,
                write is { Uncertain: true } or { Submitted: true } ? CommitSettlement.Indeterminate : null);
        }
        finally
        {
            commitToken?.Dispose();
            if (held) _submissionGate.Release();
            Exit();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await FenceAsync();
        lock (_sync)
        {
            if (_disposed) return;
            if (_activeCalls != 0 || _outstanding.Any(task => !task.IsCompleted) ||
                Unsettled(_freeze) || (_publication is not null && Unsettled(_publication)))
                throw new InvalidOperationException("october_publication_settlement_unconfirmed");
            _disposed = true;
        }
        _deadline.Dispose();
        _beforeStop.Dispose();
        _runGate.Dispose();
        _submissionGate.Dispose();
    }

    private async Task<OctoberScanReport> RunAsync(bool reconcile, CancellationToken cancellationToken)
    {
        if (!Enter()) return Report(OctoberPublicationStatus.Failed, "phase_closed", disclose: false);
        bool held = false;
        try
        {
            using var waitBound = new CancellationTokenSource(TimeSpan.FromSeconds(5), _time);
            using var queued = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, waitBound.Token);
            await _runGate.WaitAsync(queued.Token);
            held = true;
            if (_request.RequestedStage != "cl-only")
                return Report(OctoberPublicationStatus.Failed, "unsupported_stage", disclose: false);
            if (reconcile && !_executionStarted)
                return Report(OctoberPublicationStatus.Failed, "publication_not_started", disclose: false);
            _executionStarted = true;
            _writer ??= _createWriter(this, _freezeAuthority);
            if (await DisclosureAsync(cancellationToken) != DisclosureStatus.Allowed)
                return Report(OctoberPublicationStatus.Failed, "authority_unavailable", disclose: false);

            var freeze = await WriteOrLookupAsync(_freeze,
                token => _writer.FreezeCorpusAsync(_freezeRequest, token), cancellationToken);
            if (!HasConfirmedHandle(_freeze, freeze)) return await FailedResultAsync(freeze, cancellationToken);
            if (Unsettled(_freeze))
                return Report(OctoberPublicationStatus.Indeterminate, "freeze_settlement_pending", disclose: false);
            CorpusHandle corpus = freeze.Corpus!;
            if (_request.AcceptedFiles.SelectMany(file => file.Documents).Any())
            {
                if (_publication is null)
                {
                    var publication = new PublicationRequest(_request.PublicationOperation, corpus, "",
                        OctoberKnowledgePublicationPolicy.OrderedDocuments(_request.AcceptedFiles.SelectMany(file => file.Documents).ToImmutableArray()),
                        [], _request.Uncertainties, [], _request.TerminalCoverage);
                    _publicationRequest = publication with
                    {
                        CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(publication)
                    };
                    lock (_sync) _publication = new(_publicationRequest.Operation, _publicationRequest.CanonicalPayloadDigest);
                }
                var published = await WriteOrLookupAsync(_publication,
                    token => _writer.PublishSelectionAsync(_publicationRequest!, token), cancellationToken);
                if (!HasConfirmedHandle(_publication, published)) return await FailedResultAsync(published, cancellationToken);
                if (Unsettled(_publication))
                    return Report(OctoberPublicationStatus.Indeterminate, "publication_settlement_pending", disclose: false);
                if (await DisclosureAsync(cancellationToken) != DisclosureStatus.Allowed)
                    return Report(OctoberPublicationStatus.Failed, "authority_unavailable", disclose: false);
                return Report(OctoberPublicationStatus.Confirmed,
                    published.Status == KnowledgeWriteStatus.LogUnavailable ? "operation_log_unavailable" : null,
                    publication: published.Publication);
            }
            if (await DisclosureAsync(cancellationToken) != DisclosureStatus.Allowed)
                return Report(OctoberPublicationStatus.Failed, "authority_unavailable", disclose: false);
            if (_hardStop.IsCancellationRequested)
                return Report(HasUnresolvedWrite() ? OctoberPublicationStatus.Indeterminate : OctoberPublicationStatus.NotRequested,
                    "publication_interrupted", interrupted: true);
            return Report(OctoberPublicationStatus.NoContent, "no_cl_content");
        }
        catch (OperationCanceledException)
        {
            return Report(HasUnresolvedWrite() ? OctoberPublicationStatus.Indeterminate : OctoberPublicationStatus.NotRequested,
                "publication_interrupted", disclose: false, interrupted: true);
        }
        catch (Exception)
        {
            return Report(HasUnresolvedWrite() ? OctoberPublicationStatus.Indeterminate : OctoberPublicationStatus.Failed,
                "publication_unavailable", disclose: false);
        }
        finally { if (held) _runGate.Release(); Exit(); }
    }

    private async Task<KnowledgeWriteResult> WriteOrLookupAsync(PhaseWrite phase,
        Func<CancellationToken, Task<KnowledgeWriteResult>> write, CancellationToken cancellationToken)
    {
        if (phase.Outcome is { } known && HasConfirmedHandle(phase, known)) return known;
        if (!phase.Started)
        {
            var attempt = new CommitAttempt(phase.Operation, phase.Operation.OperationId, phase.Digest);
            var eligible = await CheckBeforeTransactionAsync(attempt, cancellationToken);
            if (eligible != CommitEligibilityStatus.Eligible)
                return new(eligible == CommitEligibilityStatus.Cancelled ? KnowledgeWriteStatus.CancelledBeforeCommit :
                    eligible == CommitEligibilityStatus.Denied ? KnowledgeWriteStatus.Denied :
                    eligible == CommitEligibilityStatus.AuthorityUnavailable ? KnowledgeWriteStatus.AuthorityUnavailable : KnowledgeWriteStatus.StaleOrFenced,
                    phase.Operation);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token, _hardStop);
            phase.Started = true;
            Task<KnowledgeWriteResult> pending = ObserveWriteAsync(phase, write, linked.Token);
            phase.Write = pending;
            try { return await BoundedAsync(pending, linked.Token); }
            catch (OperationCanceledException) { return new(KnowledgeWriteStatus.CommitIndeterminate, phase.Operation); }
        }
        if (phase.Outcome is { } rejected && rejected.Status != KnowledgeWriteStatus.CommitIndeterminate && !Unsettled(phase))
            return rejected;
        using var lookupBound = new CancellationTokenSource(TimeSpan.FromSeconds(5), _time);
        using var lookupToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lookupBound.Token);
        long start = _time.GetTimestamp();
        try
        {
            var result = await BoundedAsync(_writer!.LookupOperationAsync(_request.Binding,
                phase.Operation, phase.Digest, lookupToken.Token), lookupToken.Token);
            RecordOutcome(phase, result, definitiveWrite: false);
            return result;
        }
        finally { _storeWait += _time.GetElapsedTime(start); }
    }

    private async Task<KnowledgeWriteResult> ObserveWriteAsync(PhaseWrite phase,
        Func<CancellationToken, Task<KnowledgeWriteResult>> write, CancellationToken cancellationToken)
    {
        long start = _time.GetTimestamp();
        try
        {
            var result = await write(cancellationToken);
            RecordOutcome(phase, result, definitiveWrite: true);
            return result;
        }
        catch (Exception)
        {
            lock (_sync)
            {
                if (phase.Settlement is not (CommitSettlement.Committed or CommitSettlement.ConfirmedNotCommitted))
                    phase.Uncertain = true;
            }
            return new(KnowledgeWriteStatus.CommitIndeterminate, phase.Operation);
        }
        finally { lock (_sync) _storeWait += _time.GetElapsedTime(start); }
    }

    private void RecordOutcome(PhaseWrite phase, KnowledgeWriteResult result, bool definitiveWrite)
    {
        lock (_sync)
        {
            if (result.Operation != phase.Operation) { phase.Uncertain = true; _fenced = true; return; }
            if (result.Status is KnowledgeWriteStatus.Denied or KnowledgeWriteStatus.AuthorityUnavailable) _fenced = true;
            if (ValidHandle(phase, result) && result.Status is (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable))
            {
                phase.Settlement = CommitSettlement.Committed;
                phase.Uncertain = false;
                phase.Outcome = result;
            }
            else if (phase.Settlement != CommitSettlement.Committed && definitiveWrite && !phase.Submitted && !phase.Uncertain &&
                result.Status is (KnowledgeWriteStatus.Denied or KnowledgeWriteStatus.AuthorityUnavailable or
                    KnowledgeWriteStatus.InvalidProposal or KnowledgeWriteStatus.NotCompleted or KnowledgeWriteStatus.StaleOrFenced or
                    KnowledgeWriteStatus.CancelledBeforeCommit or KnowledgeWriteStatus.StorageUnavailable or KnowledgeWriteStatus.Conflict))
            {
                phase.Settlement = CommitSettlement.ConfirmedNotCommitted;
                phase.Outcome = result;
            }
            else if (phase.Outcome is null)
            {
                phase.Outcome = result;
                if (result.Status is KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable)
                {
                    phase.Uncertain = true;
                    _fenced = true;
                }
            }
        }
    }

    private bool ValidHandle(PhaseWrite phase, KnowledgeWriteResult result) => result.Operation == phase.Operation &&
        (phase.Operation.Kind == OperationKind.FreezeCorpus
            ? result.Corpus is { } corpus && corpus.Binding == _request.Binding && corpus.CorpusId != Guid.Empty && corpus.CorpusDigest == phase.Digest
            : result.Publication is { } publication && publication.Knowledge == _request.Binding && publication.PublicationId != Guid.Empty);

    private bool HasConfirmedHandle(PhaseWrite phase, KnowledgeWriteResult result) =>
        (result.Status is (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted) ||
            result.Status == KnowledgeWriteStatus.LogUnavailable && phase.Operation.Kind == OperationKind.PublishSelection) &&
        ValidHandle(phase, result);

    private async Task<CommitSettlement> ObserveCommitAsync(PhaseWrite phase, Task<CommitSettlement> settlement,
        CancellationTokenSource commitToken)
    {
        try
        {
            CommitSettlement result;
            try { result = await settlement; }
            catch (Exception) { result = CommitSettlement.Indeterminate; }
            if (!Enum.IsDefined(result)) result = CommitSettlement.Indeterminate;
            lock (_sync) if (phase.Settlement != CommitSettlement.Committed) phase.Settlement = result;
            return result;
        }
        finally { commitToken.Dispose(); }
    }

    private async Task<CommitEligibilityStatus> LiveAsync(CancellationToken cancellationToken)
    {
        var authority = await CheckAuthorityAsync(cancellationToken);
        if (authority != DisclosureStatus.Allowed)
            return authority == DisclosureStatus.Denied ? CommitEligibilityStatus.Denied : CommitEligibilityStatus.AuthorityUnavailable;
        var freeze = await BoundedAsync(_freezeAuthority.VerifyAsync(_freezeRequest, cancellationToken), cancellationToken);
        if (freeze == CorpusFreezeEligibility.Eligible) return CommitEligibilityStatus.Eligible;
        if (freeze is CorpusFreezeEligibility.Denied or CorpusFreezeEligibility.AuthorityUnavailable or CorpusFreezeEligibility.HardStopped)
            LoseAuthority();
        return freeze == CorpusFreezeEligibility.Denied ? CommitEligibilityStatus.Denied :
            freeze == CorpusFreezeEligibility.AuthorityUnavailable ? CommitEligibilityStatus.AuthorityUnavailable : CommitEligibilityStatus.StaleOrFenced;
    }

    private async Task<DisclosureStatus> DisclosureAsync(CancellationToken cancellationToken)
    {
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(5), _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bound.Token);
        try { return await CheckAuthorityAsync(linked.Token); }
        catch (Exception) { LoseAuthority(); return DisclosureStatus.AuthorityUnavailable; }
    }

    private async Task<DisclosureStatus> CheckAuthorityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await BoundedAsync(_checkAuthority(cancellationToken), cancellationToken);
            if (!Enum.IsDefined(status)) status = DisclosureStatus.AuthorityUnavailable;
            if (status != DisclosureStatus.Allowed) LoseAuthority();
            return status;
        }
        catch { LoseAuthority(); throw; }
    }

    private async Task<OctoberScanReport> FailedResultAsync(KnowledgeWriteResult result, CancellationToken cancellationToken)
    {
        if (result.Status is KnowledgeWriteStatus.Denied or KnowledgeWriteStatus.AuthorityUnavailable)
            return Report(OctoberPublicationStatus.Failed, "authority_unavailable", disclose: false);
        if (await DisclosureAsync(cancellationToken) != DisclosureStatus.Allowed)
            return Report(OctoberPublicationStatus.Failed, "authority_unavailable", disclose: false);
        bool interrupted = result.Status == KnowledgeWriteStatus.CancelledBeforeCommit ||
            (result.Status == KnowledgeWriteStatus.StaleOrFenced && _hardStop.IsCancellationRequested);
        bool indeterminate = result.Status == KnowledgeWriteStatus.CommitIndeterminate ||
            (interrupted && HasUnresolvedWrite());
        return Report(indeterminate ? OctoberPublicationStatus.Indeterminate :
                interrupted ? OctoberPublicationStatus.NotRequested : OctoberPublicationStatus.Failed,
            result.Status switch
            {
                KnowledgeWriteStatus.CommitIndeterminate => "commit_indeterminate",
                KnowledgeWriteStatus.LogUnavailable => "operation_log_unavailable",
                KnowledgeWriteStatus.Denied or KnowledgeWriteStatus.AuthorityUnavailable => "authority_unavailable",
                KnowledgeWriteStatus.StaleOrFenced => "phase_ineligible",
                _ => "publication_failed"
            }, interrupted: interrupted);
    }

    private OctoberScanReport Report(OctoberPublicationStatus status, string? gap = null,
        bool disclose = true, PublicationBinding? publication = null, bool interrupted = false)
    {
        var coverage = !disclose || _request.TerminalCoverage.IsEmpty ? OctoberCoverageStatus.None :
            _request.SelectedScopeComplete && _request.TerminalCoverage.All(item =>
                item.Disposition is CoverageDisposition.Accepted or CoverageDisposition.CompletedZeroUnit)
                ? OctoberCoverageStatus.Complete : OctoberCoverageStatus.Partial;
        var execution = status == OctoberPublicationStatus.Indeterminate ? OctoberExecutionStatus.Indeterminate :
            interrupted ? OctoberExecutionStatus.Interrupted :
            gap == "operation_log_unavailable" ? OctoberExecutionStatus.Failed :
            status is OctoberPublicationStatus.Confirmed or OctoberPublicationStatus.NoContent
                ? OctoberExecutionStatus.Completed : OctoberExecutionStatus.Failed;
        ImmutableArray<DocumentHandle> documents = disclose && publication is not null
            ? _request.AcceptedFiles.SelectMany(file => file.Documents).Select(document =>
                new DocumentHandle(document.BatchId, publication.PublicationId, document.LogicalDocumentId, document.DocumentVersionId)).ToImmutableArray()
            : [];
        return new(_request.RequestedStage, status == OctoberPublicationStatus.Confirmed ? "cl-only" : null,
            coverage, execution, status, disclose ? publication : null, documents,
            disclose ? _request.AcceptedFiles : [], disclose ? _request.TerminalCoverage : [],
            new(_time.GetElapsedTime(_started), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, _storeWait),
            new(0, 0, 0, false), gap is null ? [] : [new(gap)]);
    }

    private CommitEligibilityStatus Eligibility(CommitAttempt attempt, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_disposed || _fenced || _beforeStop.IsCancellationRequested || _hardStop.IsCancellationRequested ||
                _time.GetElapsedTime(_started) >= _lifetime) return CommitEligibilityStatus.StaleOrFenced;
            if (cancellationToken.IsCancellationRequested) return CommitEligibilityStatus.Cancelled;
            return _executionStarted && _request.RequestedStage == "cl-only" && Find(attempt) is not null
                ? CommitEligibilityStatus.Eligible : CommitEligibilityStatus.Denied;
        }
    }

    private PhaseWrite? Find(CommitAttempt attempt) => attempt is null || attempt.AttemptId != attempt.Operation.OperationId ? null :
        attempt.Operation == _freeze.Operation && attempt.PayloadDigest == _freeze.Digest ? _freeze :
        _publication is { } publication && attempt.Operation == publication.Operation && attempt.PayloadDigest == publication.Digest ? publication : null;

    private CommitEligibilityStatus CancelStatus(CommitAttempt attempt, CancellationToken token)
    {
        var current = Eligibility(attempt, token);
        return current == CommitEligibilityStatus.Eligible ? CommitEligibilityStatus.AuthorityUnavailable : current;
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

    private static bool Unsettled(PhaseWrite phase) => phase.Uncertain ||
        phase.Settlement == CommitSettlement.Indeterminate || phase.Commit is { IsCompleted: false } ||
        phase.Write is { IsCompleted: false };
    private bool HasUnresolvedWrite() { lock (_sync) return Unsettled(_freeze) || (_publication is not null && Unsettled(_publication)); }
    private void LoseAuthority() { lock (_sync) _fenced = true; }
    private bool Enter() { lock (_sync) { if (_disposed) return false; _activeCalls++; return true; } }
    private void Exit() { lock (_sync) _activeCalls--; }
    private CancellationTokenSource BeforeToken(CancellationToken caller) =>
        CancellationTokenSource.CreateLinkedTokenSource(caller, _deadline.Token, _hardStop, _beforeStop.Token);
    private static CommitGuardResult GuardFailure(CommitEligibilityStatus status) => new(status switch
    {
        CommitEligibilityStatus.Denied => CommitSubmissionStatus.Denied,
        CommitEligibilityStatus.AuthorityUnavailable => CommitSubmissionStatus.AuthorityUnavailable,
        CommitEligibilityStatus.Cancelled => CommitSubmissionStatus.CancelledBeforeSubmission,
        _ => CommitSubmissionStatus.StaleOrFenced
    }, null);

    private sealed class PhaseWrite(OperationKey operation, string digest)
    {
        internal OperationKey Operation { get; } = operation;
        internal string Digest { get; } = digest;
        internal bool Started { get; set; }
        internal bool Submitted { get; set; }
        internal bool Uncertain { get; set; }
        internal CommitSettlement? Settlement { get; set; }
        internal Task<CommitSettlement>? Commit { get; set; }
        internal Task<KnowledgeWriteResult>? Write { get; set; }
        internal KnowledgeWriteResult? Outcome { get; set; }
    }
}
