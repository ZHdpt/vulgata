using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

internal sealed partial class OctoberGenerateRunner
{
    /// <summary>Exact original-run registry; model output and replay cannot register writes.</summary>
    private sealed class BusinessCommitGuard(OctoberGenerateRunner owner) : IOctoberCommitSubmissionGuard, IAsyncDisposable
    {
        private readonly Dictionary<OperationKey, BusinessWrite> _writes = [];
        private readonly SemaphoreSlim _submission = new(1, 1);
        private bool _closed;

        internal bool HasUnsettledWrites
        {
            get
            {
                lock (owner._sync) return _writes.Values.Any(write => write.Uncertain ||
                    write.Commit is { IsCompleted: false } || write.Settlement == CommitSettlement.Indeterminate);
            }
        }

        internal void Register(CommitAttempt attempt)
        {
            lock (owner._sync)
            {
                if (_closed || !owner.BusinessRunAvailable || attempt.Operation.BatchId != owner._binding!.BatchId ||
                    attempt.Operation.Kind is not (OperationKind.FreezeCorpus or OperationKind.AcceptResolution or OperationKind.FinalizeCorpus) ||
                    attempt.Operation.OperationId == Guid.Empty || attempt.AttemptId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(attempt.PayloadDigest) || _writes.ContainsKey(attempt.Operation))
                    throw new InvalidOperationException("october_business_operation_registration_denied");
                _writes.Add(attempt.Operation, new(attempt));
            }
        }

        internal void ConfirmCommitted(OperationKey operation)
        {
            lock (owner._sync)
            {
                var write = _writes[operation];
                write.Settlement = CommitSettlement.Committed;
                write.Uncertain = false;
            }
        }

        public async Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt,
            CancellationToken cancellationToken = default)
        {
            lock (owner._sync)
            {
                var status = Eligibility(attempt, cancellationToken);
                if (status != CommitEligibilityStatus.Eligible) return status;
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner._runToken!.Token);
            try
            {
                // SubmitAsync repeats this check while the committer still holds its batch lock.
                // Recovery's exclusive lock orders abandonment after earlier writes and before later writes.
                if (owner._existingClRequest is { } existing)
                {
                    if (owner._existingClStore is null || owner._existingClAdmission is null)
                        return CommitEligibilityStatus.Denied;
                    var active = await owner._existingClStore.CheckActiveAsync(existing, owner._existingClAdmission, linked.Token);
                    if (active != CommitEligibilityStatus.Eligible) return active;
                }
                var allowed = await owner.CheckPermissionsAsync(owner._observedContributors, linked.Token);
                if (allowed != DisclosureStatus.Allowed)
                    return allowed == DisclosureStatus.Denied ? CommitEligibilityStatus.Denied : CommitEligibilityStatus.AuthorityUnavailable;
                if (attempt.Operation.Kind == OperationKind.FreezeCorpus)
                {
                    var freeze = await owner.VerifyAsync(owner._expectedFreeze!, linked.Token);
                    if (freeze != CorpusFreezeEligibility.Eligible)
                        return freeze == CorpusFreezeEligibility.AuthorityUnavailable ? CommitEligibilityStatus.AuthorityUnavailable : CommitEligibilityStatus.StaleOrFenced;
                }
                lock (owner._sync) return Eligibility(attempt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                lock (owner._sync) return Eligibility(attempt, cancellationToken) is var status && status != CommitEligibilityStatus.Eligible
                    ? status : CommitEligibilityStatus.AuthorityUnavailable;
            }
            catch (Exception) { owner.Deny(unavailable: true); return CommitEligibilityStatus.AuthorityUnavailable; }
        }

        public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt,
            Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
        {
            bool held = false;
            CancellationTokenSource? commitToken = null;
            BusinessWrite? write = null;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner._runToken!.Token);
                await _submission.WaitAsync(linked.Token);
                held = true;
                var eligible = await CheckBeforeTransactionAsync(attempt, linked.Token);
                if (eligible != CommitEligibilityStatus.Eligible) return BeginDenied(eligible);
                commitToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner._runToken.Token);
                Task<CommitSettlement> actual;
                lock (owner._sync)
                {
                    eligible = Eligibility(attempt, cancellationToken);
                    if (eligible != CommitEligibilityStatus.Eligible) return BeginDenied(eligible);
                    write = _writes[attempt.Operation];
                    if (write.Commit is not null) actual = write.Commit;
                    else if (write.Uncertain) return new(CommitSubmissionStatus.StaleOrFenced, CommitSettlement.Indeterminate);
                    else
                    {
                        write.Uncertain = true;
                        write.Settlement = CommitSettlement.Indeterminate;
                        // Final synchronous eligibility and provider submission share the host lock.
                        var submitted = submitAsync(commitToken.Token);
                        if (submitted?.Settlement is null) return new(CommitSubmissionStatus.AuthorityUnavailable, CommitSettlement.Indeterminate);
                        write.Submitted = true;
                        write.Uncertain = false;
                        actual = ObserveCommitAsync(write, submitted.Settlement, commitToken);
                        write.Commit = actual;
                        commitToken = null;
                    }
                }
                _submission.Release(); held = false;
                try { return new(CommitSubmissionStatus.Submitted, await actual.WaitAsync(linked.Token)); }
                catch (OperationCanceledException) { return new(CommitSubmissionStatus.Submitted, CommitSettlement.Indeterminate); }
            }
            catch (OperationCanceledException)
            {
                lock (owner._sync) return BeginDenied(Eligibility(attempt, cancellationToken));
            }
            catch (Exception)
            {
                return new(write?.Submitted == true ? CommitSubmissionStatus.Submitted : CommitSubmissionStatus.AuthorityUnavailable,
                    write is { Uncertain: true } or { Submitted: true } ? CommitSettlement.Indeterminate : null);
            }
            finally { commitToken?.Dispose(); if (held) _submission.Release(); }
        }

        private CommitEligibilityStatus Eligibility(CommitAttempt attempt, CancellationToken token)
        {
            if (_closed || !owner.BusinessRunAvailable) return CommitEligibilityStatus.StaleOrFenced;
            if (token.IsCancellationRequested) return CommitEligibilityStatus.Cancelled;
            if (!owner.OriginalAdmissionMatches()) return CommitEligibilityStatus.Denied;
            var handoff = owner._scope!.CheckHandoffEligibility(owner.OriginalRunId,
                owner._businessFinalizationOperation!, owner._execution!.Provider.Profile.Name);
            if (handoff != ProviderAdmissionStatus.Granted)
                return handoff switch
                {
                    ProviderAdmissionStatus.Denied => CommitEligibilityStatus.Denied,
                    ProviderAdmissionStatus.AuthorityUnavailable => CommitEligibilityStatus.AuthorityUnavailable,
                    _ => CommitEligibilityStatus.StaleOrFenced
                };
            return _writes.TryGetValue(attempt.Operation, out var write) && write.Attempt == attempt
                ? CommitEligibilityStatus.Eligible : CommitEligibilityStatus.Denied;
        }

        private async Task<CommitSettlement> ObserveCommitAsync(BusinessWrite write, Task<CommitSettlement> actual,
            CancellationTokenSource commitToken)
        {
            try
            {
                CommitSettlement settled;
                try { settled = await actual; } catch (Exception) { settled = CommitSettlement.Indeterminate; }
                lock (owner._sync) if (write.Settlement != CommitSettlement.Committed) write.Settlement = settled;
                return settled;
            }
            finally { commitToken.Dispose(); }
        }

        internal async Task ObservePendingAsync()
        {
            Task<CommitSettlement>[] tasks;
            lock (owner._sync) tasks = _writes.Values.Where(write => write.Commit is not null).Select(write => write.Commit!).ToArray();
            await Task.WhenAll(tasks);
        }

        public ValueTask DisposeAsync()
        {
            lock (owner._sync)
            {
                if (_closed) return ValueTask.CompletedTask;
                if (HasUnsettledWrites) throw new InvalidOperationException("october_business_write_settlement_unconfirmed");
                _closed = true;
            }
            _submission.Dispose();
            return ValueTask.CompletedTask;
        }

        private sealed class BusinessWrite(CommitAttempt attempt)
        {
            internal CommitAttempt Attempt { get; } = attempt;
            internal Task<CommitSettlement>? Commit { get; set; }
            internal CommitSettlement? Settlement { get; set; }
            internal bool Uncertain { get; set; }
            internal bool Submitted { get; set; }
        }
    }
}
