using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

internal sealed partial class OctoberGenerateRunner
{
    private BusinessFromClRequest? _existingClRequest;
    private BusinessFromClSnapshot? _existingClSnapshot;
    private BusinessFromClAdmission? _existingClAdmission;
    private IOctoberBusinessFromClStore? _existingClStore;
    private BusinessFromClCompletionRequest? _existingClCompletion;
    private bool _existingClCloseEligible;
    private bool _existingClSemanticUncertain;
    private bool _existingClSemanticStarted;
    private Guid OriginalRunId => _existingClRequest?.RunId ?? _beginRequest!.RunId;

    private async Task<OctoberScanReport> RunExistingClBusinessAsync(Guid runId, CancellationToken token)
    {
        var execution = _execution!;
        var request = new BusinessFromClRequest(new(_binding!, execution.Policy.ExistingClPublicationId!.Value),
            execution.Session.SubjectId, runId, "");
        _existingClRequest = request with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(request) };
        _existingClStore = _dependencies.CreateBusinessFromClStore!(this, this, new ExistingClCompletionGuard(this), this);
        BusinessFromClResult admitted;
        try
        {
            admitted = await StoreAsync(() => _existingClStore.BeginAsync(_existingClRequest, token), token);
            if (admitted.Status == KnowledgeWriteStatus.CommitIndeterminate && RunAvailable)
                admitted = await StoreAsync(() => _existingClStore.LookupAsync(_existingClRequest, token), token);
        }
        finally { lock (_sync) _beginClosed = true; }
        if (admitted.Operation == OctoberBusinessFromClPolicy.BeginOperation(_existingClRequest) && _beginSubmitted)
            _existingClAdmission = admitted.Admission;
        if (admitted.Operation != OctoberBusinessFromClPolicy.BeginOperation(_existingClRequest) ||
            !KnownCommitted(admitted.Status) || admitted.Admission is null || admitted.Snapshot is not { } snapshot ||
            !_beginSubmitted || _beginCommit is not { IsCompleted: true } ||
            snapshot.Publication != request.Publication || snapshot.Corpus.Binding != _binding ||
            snapshot.AcceptedFiles.IsDefaultOrEmpty || snapshot.Coverage.IsDefaultOrEmpty || snapshot.Contributors.IsDefault ||
            snapshot.ExistingResolutions.IsDefault)
            return BusinessFailure("business_existing_cl_admission_unavailable",
                indeterminate: admitted.Status == KnowledgeWriteStatus.CommitIndeterminate, disclose: false);

        _existingClAdmission = admitted.Admission;
        _existingClSnapshot = snapshot;
        lock (_sync) { _beginSettlement = CommitSettlement.Committed; _beginUncertain = false; _admissionClosed = true; }
        _accepted = snapshot.AcceptedFiles;
        _coverage = snapshot.Coverage;
        IncludeObservedContributors(snapshot.Contributors);
        if (admitted.Status == KnowledgeWriteStatus.LogUnavailable) return BusinessFailure("operation_log_unavailable");
        if (!BusinessRunAvailable || await CheckPermissionsAsync(_observedContributors, token) != DisclosureStatus.Allowed)
            return BusinessFailure("business_authority_unavailable", disclose: false);
        _existingClSemanticStarted = true;
        return await RunBusinessCompletionAsync(token);
    }

    Task<DisclosureStatus> IOctoberBusinessFromClAuthority.CheckAsync(BusinessFromClRequest request,
        ImmutableArray<Contributor> contributors, CancellationToken token) =>
        request == _existingClRequest && request.SubjectId == _execution?.Session.SubjectId &&
        request.Publication.Knowledge == _binding
            ? CheckPermissionsAsync(contributors, token) : Task.FromResult(DisclosureStatus.Denied);

    private bool OriginalAdmissionMatches()
    {
        if (_existingClRequest is { } existing)
            return _existingClAdmission is not null && _existingClSnapshot is { } snapshot && snapshot.Corpus == _confirmedBusinessCorpus &&
                existing.Publication == snapshot.Publication && existing.Publication.Knowledge == _binding &&
                existing.SubjectId == _execution!.Session.SubjectId &&
                existing.CanonicalPayloadDigest == OctoberBusinessFromClPolicy.ComputeBeginDigest(existing);
        return _beginRequest is { } begin && begin.Binding == _binding && begin.SubjectId == _execution!.Session.SubjectId &&
            begin.CanonicalPayloadDigest == OctoberGenerateAdmissionPolicy.ComputePayloadDigest(begin);
    }

    private async Task<OctoberScanReport> CloseExistingClBusinessAsync(OctoberScanReport report)
    {
        if (_existingClAdmission is null || _existingClStore is null || _existingClRequest is null) return report;
        // No dispatch can survive resource settlement; never reopen semantic guards for bookkeeping.
        bool knownAdmissionOnly = !_existingClSemanticStarted && _beginSubmitted &&
            _beginCommit is { IsCompletedSuccessfully: true } && _beginCommit.Result == CommitSettlement.Committed;
        _existingClCloseEligible = _resourcesSettled && _cleanupFailure is null &&
            (report.Execution != OctoberExecutionStatus.Indeterminate || knownAdmissionOnly) && !_existingClSemanticUncertain;
        if (!_existingClCloseEligible)
            return report with { Execution = OctoberExecutionStatus.Indeterminate,
                Gaps = report.Gaps.Add(new("business_attempt_unsettled")) };
        var completion = new BusinessFromClCompletionRequest(_existingClRequest, "");
        _existingClCompletion = completion with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeCompletionDigest(completion) };
        KnowledgeWriteResult result;
        try
        {
            result = await _existingClStore.CompleteAsync(_existingClCompletion, _existingClAdmission, CancellationToken.None);
            if (result.Status == KnowledgeWriteStatus.CommitIndeterminate)
                result = await _existingClStore.LookupCompletionAsync(_existingClCompletion, _existingClAdmission, CancellationToken.None);
        }
        catch (Exception)
        {
            result = new(KnowledgeWriteStatus.CommitIndeterminate, OctoberBusinessFromClPolicy.CompleteOperation(_existingClRequest));
        }
        if (result.Operation != OctoberBusinessFromClPolicy.CompleteOperation(_existingClRequest) || !KnownCommitted(result.Status))
            return report with { Execution = OctoberExecutionStatus.Indeterminate,
                Gaps = report.Gaps.Add(new("business_attempt_close_unconfirmed")) };
        return result.Status != KnowledgeWriteStatus.LogUnavailable ? report : report with
        { Execution = OctoberExecutionStatus.Failed, Gaps = report.Gaps.Add(new("operation_log_unavailable")) };
    }

    Task<BusinessFromClSettlement> IOctoberBusinessFromClSettlementAuthority.VerifyAsync(
        BusinessFromClCompletionRequest request, BusinessFromClAdmission admission, CancellationToken token)
    {
        lock (_sync)
        {
            bool exact = !token.IsCancellationRequested && request == _existingClCompletion &&
                ReferenceEquals(admission, _existingClAdmission) && _existingClCloseEligible && _resourcesSettled &&
                _originalBusinessCleanupStarted && _analysisCleanup is { IsCompletedSuccessfully: true } &&
                !_existingClSemanticUncertain && !Unresolved() && !HasOutstandingCalls() &&
                _businessOwnerCalls.All(task => task.IsCompleted) && _scope?.GetSnapshot().OutstandingAttempts == 0;
            return Task.FromResult(exact ? BusinessFromClSettlement.Settled : BusinessFromClSettlement.Unsettled);
        }
    }

    /// <summary>Original-owner terminal receipt only: no provider, corpus or semantic write authority.</summary>
    private sealed class ExistingClCompletionGuard(OctoberGenerateRunner owner) : IOctoberCommitSubmissionGuard
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Task<CommitSettlement>? _commit;

        public async Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken token = default)
        {
            if (owner._existingClCompletion is not { } completion || owner._existingClAdmission is not { } admission ||
                attempt.Operation != OctoberBusinessFromClPolicy.CompleteOperation(completion.Begin) ||
                attempt.AttemptId != completion.Begin.RunId || attempt.PayloadDigest != completion.CanonicalPayloadDigest)
                return CommitEligibilityStatus.Denied;
            return await ((IOctoberBusinessFromClSettlementAuthority)owner).VerifyAsync(completion, admission, token) == BusinessFromClSettlement.Settled
                ? CommitEligibilityStatus.Eligible : CommitEligibilityStatus.StaleOrFenced;
        }

        public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt,
            Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken token = default)
        {
            await _gate.WaitAsync(token);
            try
            {
                if (await CheckBeforeTransactionAsync(attempt, token) != CommitEligibilityStatus.Eligible)
                    return new(CommitSubmissionStatus.StaleOrFenced, null);
                if (_commit is null)
                {
                    // The closed host cannot become eligible for semantic work again.
                    token.ThrowIfCancellationRequested();
                    _commit = submitAsync(token).Settlement;
                }
                try { return new(CommitSubmissionStatus.Submitted, await _commit.WaitAsync(token)); }
                catch (Exception) { return new(CommitSubmissionStatus.Submitted, CommitSettlement.Indeterminate); }
            }
            finally { _gate.Release(); }
        }
    }
}
