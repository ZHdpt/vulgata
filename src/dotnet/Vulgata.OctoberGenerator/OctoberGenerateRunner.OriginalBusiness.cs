using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

internal sealed partial class OctoberGenerateRunner
{
    // The optional internal Business route retains the original live scope after
    // confirmed freeze. The CL-only command does not populate or issue this slot.
    private FinalizationRequest? _originalBusinessRequest = null;
    private CorpusHandle? _confirmedBusinessCorpus = null;
    private BusinessProviderConfigurationProjection? _businessProviderConfiguration = null;
    private IOctoberOriginalBusinessFinalizationSlot? _originalBusinessSlot = null;
    private bool _originalBusinessCleanupStarted;

    private IOctoberOriginalBusinessFinalizationSlot? TryIssueOriginalBusinessFinalizationSlot()
    {
        lock (_sync)
        {
            if (_originalBusinessSlot is not null) return _originalBusinessSlot;
            if (_originalBusinessRequest is not { } request || !OriginalBusinessEligible(request)) return null;
            return _originalBusinessSlot = new OriginalBusinessSlot(this, request, _runToken!.Token);
        }
    }

    // Called under the runner lock. A stored receipt alone cannot establish this
    // original process's submission or retain its scope and authenticated session.
    private bool OriginalBusinessEligible(FinalizationRequest request)
    {
        if (_execution is null || _execution.Policy.OutputStage != "cl-and-bl" ||
            _execution.Session.ExpiresAtUtc <= _time.GetUtcNow() ||
            _executionTask is not { IsCompleted: false } || !RunAvailable ||
            _runToken is null || _runToken.IsCancellationRequested || _resourcesSettled ||
            _originalBusinessCleanupStarted || _analysisCleanup is not null ||
            !_beginSubmitted || !_beginClosed || _beginCommit is not { IsCompleted: true } ||
            _beginSettlement != CommitSettlement.Committed || !_admissionClosed || Unresolved() ||
            HasOutstandingCalls() || _attempts.Any(attempt => !attempt.IsSettledForFreeze) ||
            _scope is null || _scope.GetSnapshot().OutstandingAttempts != 0 ||
            !ReferenceEquals(request.ExecutionScope, _scope) || request.Selection is not { } selection ||
            _beginRequest is null || _beginRequest.Binding != _binding || request.Corpus is null || request.Corpus.Binding != _binding ||
            _beginRequest.SubjectId != _execution.Session.SubjectId ||
            _binding != new KnowledgeBinding(_execution.Authority.CanonicalDatabaseIdentity,
                _execution.Authority.TargetId, _execution.Authority.BatchId, _execution.Authority.SelectionDigest) ||
            _confirmedBusinessCorpus != request.Corpus || selection.RunId != _beginRequest.RunId ||
            _businessProviderConfiguration is not { } configuration ||
            configuration.ProfileName != selection.ProviderProfileId ||
            _originalBusinessRequest is not { } retained || !SameBusinessRequest(retained, request))
            return false;
        try
        {
            if (_beginRequest.CanonicalPayloadDigest != OctoberGenerateAdmissionPolicy.ComputePayloadDigest(_beginRequest) ||
                selection.ProviderConfigurationFingerprint != OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(configuration))
                return false;
        }
        catch (ArgumentException) { return false; }
        return _scope.CheckHandoffEligibility(selection.RunId, request.Operation, selection.ProviderProfileId) ==
            ProviderAdmissionStatus.Granted;
    }

    private static bool SameBusinessRequest(FinalizationRequest expected, FinalizationRequest? actual)
    {
        if (actual is null || !ReferenceEquals(expected.ExecutionScope, actual.ExecutionScope) ||
            actual.Operation != expected.Operation || actual.Corpus != expected.Corpus ||
            actual.CanonicalPayloadDigest != expected.CanonicalPayloadDigest)
            return false;
        try
        {
            return OctoberBusinessFinalizationPolicy.ComputeRequestDigest(actual) == expected.CanonicalPayloadDigest &&
                OctoberBusinessFinalizationPolicy.ComputeRequestDigest(expected) == expected.CanonicalPayloadDigest;
        }
        catch (ArgumentException) { return false; }
    }

    private async Task AwaitOriginalBusinessSettlementAsync()
    {
        Task<BusinessFinalizationResult>? retained;
        lock (_sync)
        {
            // Serializes cleanup against the first callback installation. Joining
            // existing work remains permitted; cleanup never renews a dispatch.
            _originalBusinessCleanupStarted = true;
            retained = (_originalBusinessSlot as OriginalBusinessSlot)?.RetainedTask;
        }
        if (retained is null) return;
        try { await retained; }
        catch (Exception) { /* A settled callback failure remains on its retained task. */ }
        Observe(retained);
    }

    private sealed class OriginalBusinessSlot(OctoberGenerateRunner owner, FinalizationRequest request,
        CancellationToken hostToken) : IOctoberOriginalBusinessFinalizationSlot
    {
        internal Task<BusinessFinalizationResult>? RetainedTask { get; private set; }

        public async Task<BusinessFinalizationResult> RunOnceAsync(FinalizationRequest actual,
            Func<CancellationToken, Task<BusinessFinalizationResult>> originalWork,
            CancellationToken waitCancellation = default)
        {
            TaskCompletionSource<BusinessFinalizationResult>? completion = null;
            Task<BusinessFinalizationResult> task;
            lock (owner._sync)
            {
                if (!SameBusinessRequest(request, actual)) return Denied();
                if (RetainedTask is null)
                {
                    if (originalWork is null || !owner.OriginalBusinessEligible(request)) return Denied();
                    completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    RetainedTask = completion.Task;
                    owner._outstanding.Add(RetainedTask);
                }
                task = RetainedTask;
            }
            // Even the first caller is merely a waiter. Its token cannot cancel
            // original work, and duplicate delegates can never replace it.
            if (completion is not null) _ = CompleteAsync(completion, originalWork);
            return await task.WaitAsync(waitCancellation);
        }

        private async Task CompleteAsync(TaskCompletionSource<BusinessFinalizationResult> completion,
            Func<CancellationToken, Task<BusinessFinalizationResult>> originalWork)
        {
            try { completion.TrySetResult(await originalWork(hostToken)); }
            catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
            catch (Exception error) { completion.TrySetException(error); }
            finally { owner.Observe(completion.Task); }
        }

        private BusinessFinalizationResult Denied() => new(BusinessFinalizationStatus.Denied,
            request.Operation, null, new(false, []));
    }
}
