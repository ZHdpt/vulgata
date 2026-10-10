using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Infrastructure.Data.October.Knowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>Host-owned contained access, opened only after the original run's Begin receipt.</summary>
internal interface IOctoberScanRunSource : IAsyncDisposable
{
    Task<TimeSpan> EnsureFileWindowAsync(TimeSpan requiredLifetime, CancellationToken cancellationToken = default);
    Task<PreparedSourceResult> ListSourceAsync(Guid repositoryId, PreparedSourceListRequest request,
        CancellationToken cancellationToken = default);
    Task<OctoberSourceFrameResult> AcquireFrameAsync(Guid repositoryId, string relativePath,
        CancellationToken cancellationToken = default);
    OctoberToolRuntime GetRuntime(Guid repositoryId);
}

internal sealed record OctoberKnowledgeHostPorts(
    IOctoberFileAcceptanceAuthority CompletionAuthority,
    IOctoberCommitSubmissionGuard CommitGuard,
    IOctoberCorpusFreezeAuthority FreezeAuthority,
    IOctoberKnowledgeSourceVerifier SourceVerifier,
    IOctoberKnowledgeProducerAuthorization ProducerAuthorization);

internal sealed record OctoberGenerateRunnerDependencies(
    IOctoberLocalAuthority Authority,
    Func<OctoberGenerateExecution, bool> ValidateSourceConfiguration,
    Func<IOctoberGenerateAdmissionAuthority, IOctoberCommitSubmissionGuard, IOctoberGenerateAdmissionStore> CreateAdmissionStore,
    Func<OctoberGenerateExecution, TimeSpan, CancellationToken, Task<IOctoberScanRunSource>> OpenSource,
    Func<OctoberSelectedWorkerProvider, CancellationToken, Task<IChatClient>> CreateWorkerClient,
    Func<OctoberKnowledgeHostPorts, IOctoberKnowledgeWriter> CreateKnowledgeWriter,
    IOctoberKnowledgeOperationLog OperationLog)
{
    internal OctoberBusinessRunComposition? Business { get; init; }
    internal OctoberParseFailureCapture? ParseFailureCapture { get; init; }
    internal OctoberWorkerHttpObservation? HttpObservation { get; init; }
    internal int MaximumWorkerOutputTokens { get; init; } = 8192;
}

/// <summary>Owns the original sequential Generate run; a stored receipt is never a restart grant.</summary>
internal sealed partial class OctoberGenerateRunner : IOctoberGenerateOperation, IAsyncDisposable,
    IOctoberGenerateAdmissionAuthority, IOctoberCommitSubmissionGuard, IOctoberCorpusFreezeAuthority,
    IOctoberKnowledgeProducerAuthorization, IOctoberKnowledgeSourceVerifier, IOctoberFileAcceptanceAuthority
{
    private readonly OctoberGenerateRunnerDependencies _dependencies;
    private readonly TimeProvider _time;
    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _beginGate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly HashSet<Task> _outstanding = [];
    private readonly List<OctoberAttemptState> _attempts = [];
    private readonly List<IChatClient> _clients = [];
    private Task<OctoberGeneratorExecutionResult>? _executionTask;
    private OctoberGenerateExecution? _execution;
    private KnowledgeBinding? _binding;
    private GenerateAdmissionRequest? _beginRequest;
    private Task<CommitSettlement>? _beginCommit;
    private CommitSettlement? _beginSettlement;
    private bool _beginUncertain;
    private bool _beginClosed;
    private bool _beginSubmitted;
    private bool _fenced;
    private bool _disposed;
    private bool _admissionClosed;
    private bool _resourcesSettled;
    private Exception? _cleanupFailure;
    private Task? _analysisCleanup;
    private long _started;
    private long? _fileStarted;
    private long? _sourceWindowStarted;
    private TimeSpan _sourceWindow;
    private CancellationToken _caller;
    private CancellationTokenSource? _deadline;
    private CancellationTokenSource? _runToken;
    private OctoberExecutionScope? _scope;
    private OctoberRunCorrectionBudget? _hardRetryBudget;
    private IOctoberScanRunSource? _source;
    private OctoberWorkerInputLedger? _activeLedger;
    private Guid? _activeAdmission;
    private PreparedOrigin? _activeOrigin;
    private OctoberClPublicationPhase? _publication;
    private ImmutableArray<Contributor> _observedContributors = [];
    private ImmutableArray<AcceptedFile> _accepted = [];
    private ImmutableArray<FileCoverage> _coverage = [];
    private ImmutableArray<Uncertainty> _uncertainties = [];
    private ImmutableArray<OctoberScanInventoryEntry> _inventory = [];
    private ImmutableArray<OctoberScanInventoryEntry> _unstartedFiles = [];
    private ImmutableArray<OctoberScanGap> _gaps = [];
    private CorpusFreezeRequest? _expectedFreeze;
    private TimeSpan _toolWait;
    private TimeSpan _storeWait;

    internal OctoberGenerateRunner(OctoberGenerateRunnerDependencies dependencies,
        TimeProvider? timeProvider = null)
    {
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        if (dependencies.MaximumWorkerOutputTokens is < 1 or > 393216)
            throw new ArgumentOutOfRangeException(nameof(dependencies.MaximumWorkerOutputTokens));
        ArgumentNullException.ThrowIfNull(dependencies.Authority);
        ArgumentNullException.ThrowIfNull(dependencies.ValidateSourceConfiguration);
        ArgumentNullException.ThrowIfNull(dependencies.CreateAdmissionStore);
        ArgumentNullException.ThrowIfNull(dependencies.OpenSource);
        ArgumentNullException.ThrowIfNull(dependencies.CreateWorkerClient);
        ArgumentNullException.ThrowIfNull(dependencies.CreateKnowledgeWriter);
        ArgumentNullException.ThrowIfNull(dependencies.OperationLog);
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGenerateExecution execution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        TaskCompletionSource<OctoberGeneratorExecutionResult>? completion = null;
        Task<OctoberGeneratorExecutionResult> task;
        lock (_sync)
        {
            if (_disposed || (_execution is not null && !ReferenceEquals(execution, _execution)))
                return Finished(Failure("run_binding_unavailable", disclose: false, stage: execution.Policy.OutputStage));
            if (_executionTask is null)
            {
                _execution = execution;
                _started = _time.GetTimestamp();
                _caller = cancellationToken;
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _executionTask = completion.Task;
            }
            task = _executionTask;
        }
        if (completion is not null) _ = CompleteAsync(completion);
        // The original caller token already fences execution. Keep this owning
        // task alive until cleanup settles so the module retains its session.
        OctoberGeneratorExecutionResult result = await task;
        // Returning a cached run never repeats dispatch, but remains a new disclosure.
        if (result.Report is { } report && HasMetadata(report))
        {
            DisclosureStatus disclosure = await DisclosureAsync(CancellationToken.None);
            await AwaitBusinessOwnerCallsAsync();
            if (disclosure != DisclosureStatus.Allowed)
                return Finished(Hide(report, "authority_unavailable"));
        }
        return result;
    }

    private async Task CompleteAsync(TaskCompletionSource<OctoberGeneratorExecutionResult> completion)
    {
        OctoberScanReport report;
        try { report = await RunAsync(); }
        catch (OperationCanceledException)
        { report = Failure("run_interrupted", interrupted: !Unresolved(), indeterminate: Unresolved(), disclose: false); }
        catch (Exception)
        { report = Failure("run_unavailable", indeterminate: Unresolved(), disclose: false); }
        try { await SettleResourcesAsync(); }
        catch (Exception error)
        {
            _cleanupFailure = error;
            report = report with { Execution = OctoberExecutionStatus.Indeterminate,
                Gaps = report.Gaps.Add(new("owned_cleanup_unconfirmed")) };
        }
        if (_hardRetryBudget is not null && !report.Inventory.IsEmpty)
            report = report with { HardRetry = _hardRetryBudget.Read() };
        if (HasMetadata(report) && await DisclosureAsync(CancellationToken.None) != DisclosureStatus.Allowed)
            report = Hide(report, "authority_unavailable");
        await AwaitBusinessOwnerCallsAsync();
        report = MergeMetrics(report);
        completion.TrySetResult(Finished(report));
    }

    private async Task<OctoberScanReport> RunAsync()
    {
        OctoberScanReport report = await RunCoreAsync();
        if (_activeAdmission is not { } admission || _activeOrigin is not { } origin ||
            _coverage.Any(item => item.AdmissionId == admission) || report.Inventory.IsEmpty) return report;
        if (report.Execution == OctoberExecutionStatus.Indeterminate || Unresolved())
        {
            var gap = new OctoberScanGap("active-file-unsettled", origin.RepositoryId, origin.RelativePath);
            return report with { Gaps = report.Gaps.Add(gap) };
        }
        _coverage = _coverage.Add(new(admission, origin, IsCancelled ? CoverageDisposition.Cancelled : CoverageDisposition.Failed, "worker_not_completed"));
        return report with { TerminalCoverage = _coverage };
    }

    private async Task<OctoberScanReport> RunCoreAsync()
    {
        OctoberGenerateExecution execution = _execution!;
        if (!new OctoberScanPolicyValidator().Validate(execution.Policy).IsValid ||
            (execution.Policy.OutputStage != "cl-only" && !BusinessConfigurationMatches(execution)) || execution.Policy.WorkerCap != 1 ||
            !_dependencies.ValidateSourceConfiguration(execution) || !BindingMatches(execution.Setup.Snapshot.Request, execution.Authority))
            return Failure("invalid_run_configuration", disclose: false);
        _binding = new(execution.Authority.CanonicalDatabaseIdentity, execution.Authority.TargetId,
            execution.Authority.BatchId, execution.Authority.SelectionDigest);
        _deadline = new(TimeSpan.FromSeconds(execution.Policy.RunDeadlineSeconds), _time);
        _runToken = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token, _stop.Token, _caller);
        CancellationToken token = _runToken.Token;
        Guid runId = Guid.NewGuid();
        if (execution.Policy.HardRetryMode) _hardRetryBudget = new(_binding, runId);
        bool captureBound = _dependencies.ParseFailureCapture?.TryBind(execution, runId) == true;
        bool observationBound = _dependencies.HttpObservation?.TryBind(execution, runId) == true;
        if (_dependencies.HttpObservation is not null && (!observationBound || _dependencies.ParseFailureCapture is not null))
            return Failure("invalid_run_configuration", disclose: false);
        if (execution.Policy.OutputStage == "cl-and-bl")
            _businessFinalizationOperation = new(_binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        // Construction precedes Begin and all preparation, so no later phase receives
        // a fresh run allowance. The callback also checks the original host stopwatch.
        _scope = new(runId, execution.Provider.Profile.Name, execution.Policy,
            CheckProviderEligibilityAsync, _businessFinalizationOperation, timeProvider: _time, hardStop: token);
        if (await CheckPermissionsAsync([], token) != DisclosureStatus.Allowed)
            return Failure("authority_unavailable", disclose: false);
        var begin = new GenerateAdmissionRequest(_binding, execution.Session.SubjectId, runId, Guid.NewGuid(), "");
        _beginRequest = begin with { CanonicalPayloadDigest = OctoberGenerateAdmissionPolicy.ComputePayloadDigest(begin) };
        IOctoberGenerateAdmissionStore admission = _dependencies.CreateAdmissionStore(this, this);
        KnowledgeWriteResult receipt;
        try
        {
            receipt = await StoreAsync(() => admission.StartFreshExecutionAsync(_beginRequest, token), token);
        }
        catch (Exception)
        {
            receipt = new(KnowledgeWriteStatus.CommitIndeterminate, OctoberGenerateAdmissionPolicy.OperationFor(_binding));
        }
        finally { lock (_sync) _beginClosed = true; }
        bool originalUnknown = receipt.Status == KnowledgeWriteStatus.CommitIndeterminate;
        if (originalUnknown && RunAvailable)
            receipt = await StoreAsync(() => admission.LookupAsync(_beginRequest, token), token);
        if (receipt.Operation != OctoberGenerateAdmissionPolicy.OperationFor(_binding) ||
            (receipt.Status != KnowledgeWriteStatus.Committed &&
             !(receipt.Status == KnowledgeWriteStatus.AlreadyCommitted && (originalUnknown || _beginSubmitted))))
            return Failure(receipt.Status == KnowledgeWriteStatus.CommitIndeterminate ? "begin_indeterminate" : "fresh_run_required",
                indeterminate: receipt.Status == KnowledgeWriteStatus.CommitIndeterminate, disclose: false);
        lock (_sync) { _beginSettlement = CommitSettlement.Committed; _beginUncertain = false; }
        if (!RunAvailable || await CheckPermissionsAsync([], token) != DisclosureStatus.Allowed)
            return Failure("run_ineligible", interrupted: IsCancelled, disclose: false);
        await AcquireSourceAsync(execution, token);
        var inventory = new OctoberScanInventory((repository, request, callToken) =>
            ToolAsync(() => _source.ListSourceAsync(repository, request, callToken), callToken));
        bool complete = true;
        foreach (Guid repository in execution.Policy.SourceRules.Select(rule => rule.RepositoryId).Distinct().Order())
        {
            OctoberScanInventoryResult listed = await inventory.BuildAsync(repository, "", execution.Policy.SourceRules, cancellationToken: token);
            complete &= listed.IsComplete;
            _inventory = _inventory.AddRange(listed.Entries);
            _gaps = _gaps.AddRange(listed.Gaps.Select(gap => new OctoberScanGap(gap.Reason, gap.RepositoryId, gap.RelativeDirectory)));
        }
        var selected = _inventory.Where(entry => entry.Kind == PreparedSourceEntryKind.RegularFile && entry.Disposition == "selected")
            .OrderByDescending(entry => entry.Priority).ThenBy(entry => entry.RepositoryId).ThenBy(entry => entry.RelativePath, StringComparer.Ordinal).ToArray();
        _unstartedFiles = selected.ToImmutableArray();
        if (!RunAvailable) return Failure("run_ineligible", interrupted: IsCancelled);
        try
        {
            foreach (var selectedFile in selected)
            {
                if (!RunAvailable) return Failure("run_ineligible", interrupted: IsCancelled);
                if (Unresolved() || _scope?.GetSnapshot().OutstandingAttempts != 0 || _attempts.Any(attempt => !attempt.IsSettledForFreeze))
                    return Failure("source_window_unsettled", indeterminate: true);
                try
                {
                    // Start before renewal so its latency cannot extend the returned authority window.
                    _sourceWindowStarted = _time.GetTimestamp();
                    _sourceWindow = await ToolAsync(() => _source.EnsureFileWindowAsync(
                        Min(TimeSpan.FromSeconds(execution.Policy.FileDeadlineSeconds + 30), RemainingRun), token), token);
                }
                catch (OctoberSourceWindowException error)
                {
                    return Failure("source_window_" + error.Reason.ToString().ToLowerInvariant(),
                        indeterminate: error.Reason == OctoberSourceWindowFailure.CleanupUnconfirmed,
                        disclose: error.Reason != OctoberSourceWindowFailure.AuthorityUnavailable);
                }
                OctoberSourceFrameResult acquired = await ToolAsync(() => _source.AcquireFrameAsync(
                    selectedFile.RepositoryId, selectedFile.RelativePath, token), token);
                if (acquired.Status != OctoberSourceFrameStatus.Acquired || acquired.Frame is null)
                    return Failure("source_frame_unavailable", interrupted: IsCancelled);
                IOctoberVerifiedFileFrame frame = acquired.Frame;
                var repository = execution.Setup.Snapshot.Request.Repositories.Single(item => item.RepositoryId == selectedFile.RepositoryId);
                if (frame.Origin.RepositoryId != repository.RepositoryId || frame.Origin.RootBindingIdentity != repository.RootBindingIdentity ||
                    frame.Origin.RelativePath != selectedFile.RelativePath || !await BoundedAsync(frame.VerifyCurrentAsync(token), token))
                    return Failure("source_frame_binding_changed", disclose: false);
                _unstartedFiles = _unstartedFiles.RemoveAt(0);
                _fileStarted = _time.GetTimestamp();
                Guid admissionId = Guid.NewGuid();
                _activeAdmission = admissionId;
                _activeOrigin = frame.Origin;
                Guid workstreamId = Guid.NewGuid();
                var toolBudget = new OctoberFileToolBudget(runId, workstreamId, admissionId,
                    execution.Policy.AdvisoryToolCallsPerFile, execution.Policy.MaximumToolCallsPerFile);
                ImmutableArray<OctoberWorkerAdvisory> advisory = Advisory(repository.RepositoryId);
                ImmutableArray<Contributor> baseline = [new(ContributorKind.Source, repository.RepositoryId,
                    repository.SystemId, repository.RootBindingIdentity, null, frame.Origin.SourceDigest), .. advisory.Select(item => item.Contributor)];
                IncludeObservedContributors(baseline);
                for (int retry = 0; ; retry++)
                {
                    if (!FileAvailable) return Failure("file_deadline_exceeded", interrupted: IsCancelled);
                    var request = new OctoberFileWorkerRequest(_binding, runId, workstreamId, admissionId, Guid.NewGuid(),
                        execution.Provider.Profile.Name, frame, baseline, advisory,
                        new(MaximumProviderRounds: execution.Policy.MaximumModelRequestsPerFile,
                            MaximumToolCalls: execution.Policy.MaximumToolCallsPerFile,
                            // Input includes JSON escaping, prompt, tool schemas and replayed history,
                            // not just the source window. Reserve measured input at each leaf under
                            // the existing Worker ceiling and the run's cumulative policy.
                            RequestTimeout: RemainingFile, MaximumTokensPerRequest: Math.Min(1048576L, execution.Policy.MaximumTotalTokens!.Value),
                            MaximumContinuationWindows: execution.Policy.MaximumContinuationWindows,
                            MaximumOutputTokensPerRequest: _dependencies.MaximumWorkerOutputTokens,
                            MaximumJsonCorrectionAttempts: 1,
                            AdvisoryToolCalls: execution.Policy.AdvisoryToolCallsPerFile,
                            UseTextForJsonCorrection: execution.Policy.UseTextForJsonCorrection,
                            HardRetryMode: execution.Policy.HardRetryMode))
                    { ToolBudget = toolBudget, HardRetryBudget = _hardRetryBudget };
                    var ledger = new OctoberWorkerInputLedger(request,
                        async (path, callToken) =>
                        {
                            var support = await ToolAsync(() => _source.AcquireFrameAsync(repository.RepositoryId, path, callToken), callToken);
                            return support.Status == OctoberSourceFrameStatus.Acquired ? support.Frame : null;
                        }, async (contributors, callToken) =>
                        {
                            var union = IncludeObservedContributors(contributors);
                            return await CheckPermissionsAsync(union, callToken);
                        });
                    _activeLedger = ledger;
                    request = request with { Source = ledger };
                    var workerTools = new OctoberWorkerTools(_source.GetRuntime(repository.RepositoryId), ledger);
                    IReadOnlyList<AIFunction> tools = await ToolAsync(() => workerTools.CreateAsync(token), token);
                    tools = tools.Select(tool => (AIFunction)new MeasuredTool(this, tool)).ToArray();
                    IChatClient client = execution.Policy.OutputStage == "cl-and-bl" && _businessClient is not null
                        ? _businessClient : await AcquireClientAsync(execution.Provider, token);
                    Func<string, CancellationToken, Task<OctoberWorkerCaptureStatus>>? capture = _dependencies.ParseFailureCapture is { } sink
                        ? (text, callToken) => captureBound
                            ? sink.TryCaptureAsync(execution, runId, text, async verificationToken =>
                            {
                                if (!FileAvailable || verificationToken.IsCancellationRequested || !ReferenceEquals(_execution, execution) ||
                                    !ReferenceEquals(_activeLedger, ledger) || execution.Session.ExpiresAtUtc <= _time.GetUtcNow()) return false;
                                bool current = await ledger.VerifyCurrentAsync(verificationToken);
                                return current && FileAvailable && !verificationToken.IsCancellationRequested &&
                                    ReferenceEquals(_activeLedger, ledger) && execution.Session.ExpiresAtUtc > _time.GetUtcNow();
                            }, callToken)
                            : Task.FromResult(OctoberWorkerCaptureStatus.Suppressed)
                        : null;
                    var executor = new OctoberFileWorkerExecutor(client, _scope, capture);
                    Task<OctoberFileWorkerResult>? actualWorker = null;
                    var attempt = new OctoberAttemptState(new(_binding.BatchId, OperationKind.AcceptFile, Guid.NewGuid()), request,
                        callToken => actualWorker = executor.ExecuteAsync(request, tools, callToken),
                        callToken => CheckPermissionsAsync(_observedContributors, callToken),
                        RemainingFile, ledger, _time, token);
                    _attempts.Add(attempt);
                    CompletedFileSubmission? submission = await attempt.ExecuteAndSealAsync(token);
                    // The bounded seal can stop waiting before the actual executor
                    // has finished transport cleanup. Retain that ownership first.
                    if (actualWorker is not null)
                        try { await actualWorker; } catch (Exception) { /* Settlement remains the scope's authority. */ }
                    var accounting = _scope.GetSnapshot();
                    if (accounting.HasUnconfirmedSettlement || accounting.OutstandingAttempts != 0)
                        return Failure("provider_settlement_unconfirmed", indeterminate: true);
                    if (execution.Policy.OutputStage == "cl-only" || submission is null)
                    {
                        client.Dispose();
                        _clients.Remove(client);
                        if (ReferenceEquals(_businessClient, client)) _businessClient = null;
                    }
                    else _businessClient = client;
                    if (submission is null)
                    {
                        OctoberFileWorkerResult? observed = attempt.WorkerResult;
                        bool retrySafe = observed is { Status: OctoberFileWorkerStatus.ProviderFailed,
                            RetryDisposition: OctoberWorkerRetryDisposition.KnownSafeTransient } &&
                            !accounting.Usage.HasUnknownUsage && attempt.IsSettledForFreeze && !attempt.HasSubmitted &&
                            retry < execution.Policy.MaximumTransientRetries && FileAvailable;
                        if (retrySafe)
                        {
                            TimeSpan delay = TimeSpan.FromSeconds(retry == 0 ? 1 : 3);
                            if (observed!.RetryAfter is { } retryAfter && retryAfter > delay) delay = retryAfter;
                            if (delay < TimeSpan.Zero || delay >= RemainingFile) retrySafe = false;
                            else
                            {
                                await Task.Delay(delay, _time, token);
                                if (!FileAvailable || await CheckProviderEligibilityAsync(OctoberProviderPhase.Worker, token) != ProviderAdmissionStatus.Granted)
                                    retrySafe = false;
                            }
                        }
                        if (retrySafe) continue;
                        _coverage = _coverage.Add(new(admissionId, frame.Origin, IsCancelled ? CoverageDisposition.Cancelled : CoverageDisposition.Failed, "worker_not_completed"));
                        _admissionClosed = true;
                        OctoberWorkerHttpObservationSnapshot? observation = null;
                        if (observationBound && observed is { HttpObservation: not null, FailureStage: OctoberWorkerFailureStage.ProposalParse } &&
                            Enum.IsDefined(observed.Status) && observed.Status != OctoberFileWorkerStatus.Completed)
                        {
                            try
                            {
                                if (FileAvailable && !token.IsCancellationRequested && execution.Session.ExpiresAtUtc > _time.GetUtcNow() &&
                                    await ledger.VerifyCurrentAsync(token) && FileAvailable && !token.IsCancellationRequested &&
                                    ReferenceEquals(_activeLedger, ledger) && execution.Session.ExpiresAtUtc > _time.GetUtcNow())
                                    observation = observed.HttpObservation;
                            }
                            catch (Exception) { }
                        }
                        return Failure("worker_not_completed", interrupted: IsCancelled) with
                        { WorkerDiagnostics = CreateWorkerDiagnostics(observed)! with { ToolFailure = workerTools.FirstFailure, HttpObservation = observation } };
                    }
                    IOctoberKnowledgeWriter writer = _dependencies.CreateKnowledgeWriter(new(attempt, attempt, this, ledger, this));
                    KnowledgeWriteResult accepted = await StoreAsync(() => attempt.AcceptAsync(writer, token), token);
                    if (accepted.Status == KnowledgeWriteStatus.CommitIndeterminate)
                        accepted = await StoreAsync(() => attempt.ReconcileAsync(writer, token), token);
                    if (accepted.Status is not (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted) || accepted.AcceptedFile is null)
                        return Failure("file_acceptance_unavailable", indeterminate: accepted.Status == KnowledgeWriteStatus.CommitIndeterminate || !attempt.IsSettledForFreeze,
                            disclose: accepted.Status is not (KnowledgeWriteStatus.Denied or KnowledgeWriteStatus.AuthorityUnavailable));
                    _accepted = _accepted.Add(accepted.AcceptedFile);
                    _coverage = _coverage.Add(accepted.AcceptedFile.Coverage);
                    _uncertainties = _uncertainties.AddRange(submission.Proposal.Uncertainties);
                    _activeAdmission = null;
                    _activeOrigin = null;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return Failure("run_interrupted", interrupted: !Unresolved(), indeterminate: Unresolved());
        }
        catch (Exception)
        {
            return Failure("run_unavailable", indeterminate: Unresolved());
        }
        finally { _admissionClosed = true; }
        if (execution.Policy.OutputStage == "cl-and-bl")
        {
            await CloseSourceResourcesAsync();
            OctoberScanReport business;
            try { business = await RunBusinessCompletionAsync(token); }
            catch (OperationCanceledException)
            { return BusinessFailure("run_interrupted", interrupted: !Unresolved(), indeterminate: Unresolved(), disclose: false); }
            catch (Exception)
            { return BusinessFailure("run_unavailable", indeterminate: Unresolved(), disclose: false); }
            if (await DisclosureAsync(token) != DisclosureStatus.Allowed) return Hide(business, "authority_unavailable");
            return business with { Inventory = _inventory, Gaps = business.Gaps.AddRange(_gaps),
                Coverage = !complete && business.Coverage == OctoberCoverageStatus.Complete ? OctoberCoverageStatus.Partial : business.Coverage };
        }
        // Accepted provenance and fresh permissions suffice from here. Disposable
        // source grants and native processes must settle before publication starts.
        await CloseAnalysisResourcesAsync();
        if (!RunAvailable) return Failure("run_ineligible", interrupted: IsCancelled);
        var phaseRequest = new OctoberClPublicationRequest(_binding, new(_binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()),
            new(_binding.BatchId, OperationKind.PublishSelection, Guid.NewGuid()), _accepted, _coverage, _uncertainties, complete, "cl-only");
        var freeze = new CorpusFreezeRequest(phaseRequest.FreezeOperation, _binding, "", _accepted, _coverage);
        _expectedFreeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        _publication = new(phaseRequest, (guard, freezeAuthority) => _dependencies.CreateKnowledgeWriter(new(this, guard, freezeAuthority, this, this)),
            this, callToken => CheckPermissionsAsync(_observedContributors, callToken), RemainingRun, _time, token);
        OctoberScanReport published = await _publication.ExecuteAsync(token);
        if (published.ExitCode == 4 && RunAvailable) published = await _publication.ReconcileAsync(token);
        if (await DisclosureAsync(token) != DisclosureStatus.Allowed) return Hide(published, "authority_unavailable");
        return published with { Inventory = _inventory, Gaps = published.Gaps.AddRange(_gaps) };
    }

    private ImmutableArray<OctoberWorkerAdvisory> Advisory(Guid repositoryId)
    {
        var selected = _execution!.Authority.SelectedContext!;
        var repository = selected.Repositories.Single(item => item.RepositoryId == repositoryId);
        return selected.Entries.Where(entry => entry.RepositoryId == repositoryId && entry.Applicability == OctoberContextApplicability.Selected)
            .Select(entry =>
            {
                if (entry.OriginRecordId is null || entry.Value is null) throw new InvalidOperationException("Advisory snapshot unavailable.");
                var contributor = entry.Scope switch
                {
                    OctoberContextScope.Global => new Contributor(ContributorKind.GlobalAdvisory, null, null, null, entry.OriginRecordId, OctoberKnowledgePolicy.Hash(entry.Value)),
                    OctoberContextScope.System => new Contributor(ContributorKind.SystemAdvisory, null, repository.SystemId, null, entry.OriginRecordId, OctoberKnowledgePolicy.Hash(entry.Value)),
                    OctoberContextScope.Repository => new Contributor(ContributorKind.RepositoryAdvisory, repositoryId, null, null, entry.OriginRecordId, OctoberKnowledgePolicy.Hash(entry.Value)),
                    _ => throw new InvalidOperationException("Advisory scope unavailable.")
                };
                return new OctoberWorkerAdvisory(contributor, entry.Value);
            }).Distinct().ToImmutableArray();
    }

    private static OctoberWorkerDiagnosticSnapshot? CreateWorkerDiagnostics(OctoberFileWorkerResult? result)
    {
        if (result is null) return new(null, null, OctoberWorkerFailureStage.HostSealing);
        return new(Enum.IsDefined(result.Status) ? result.Status : null,
            Enum.IsDefined(result.RetryDisposition) ? result.RetryDisposition : null,
            result.Status == OctoberFileWorkerStatus.Completed ? OctoberWorkerFailureStage.HostSealing :
                result.FailureStage is { } stage && Enum.IsDefined(stage) ? stage : null)
        {
            TerminalFailure = Enum.IsDefined(result.Status) && result.Status != OctoberFileWorkerStatus.Completed &&
                result.TerminalFailure is { } terminal && Enum.IsDefined(terminal) ? terminal : null,
            TerminalObservation = result.Status == OctoberFileWorkerStatus.InvalidOutput &&
                result.FailureStage == OctoberWorkerFailureStage.StreamTerminal &&
                result.TerminalFailure == OctoberWorkerTerminalFailure.UnsupportedFinish && result.TerminalObservation is { } observed
                ? new(observed.FinishReason is { } finish && Enum.IsDefined(finish) ? finish : null,
                    observed.MaximumOutputTokens is > 0 and <= 393216 ? observed.MaximumOutputTokens : null,
                    observed.InputTokens is >= 0 ? observed.InputTokens : null,
                    observed.OutputTokens is >= 0 ? observed.OutputTokens : null,
                    observed.ReasoningTokens is >= 0 ? observed.ReasoningTokens : null) : null,
            BudgetFailure = result.Status == OctoberFileWorkerStatus.BudgetExceeded &&
                result.BudgetFailure is { } budget && Enum.IsDefined(budget) ? budget : null,
            ParseFailure = Enum.IsDefined(result.Status) && result.Status != OctoberFileWorkerStatus.Completed &&
                result.FailureStage == OctoberWorkerFailureStage.ProposalParse && result.ParseFailure is { } parse
                ? new(parse.Category is { } category && Enum.IsDefined(category) ? category : null,
                    parse.Slot is { } slot && Enum.IsDefined(slot) ? slot : null,
                    parse.FieldFailure is { } fieldFailure && Enum.IsDefined(fieldFailure) ? fieldFailure : null,
                    parse.Field is { } field && Enum.IsDefined(field) ? field : null,
                    parse.UnitIndex is >= 0 and <= 65535 ? parse.UnitIndex : null) : null,
            CaptureStatus = Enum.IsDefined(result.Status) && result.Status != OctoberFileWorkerStatus.Completed &&
                result.FailureStage == OctoberWorkerFailureStage.ProposalParse && result.CaptureStatus is { } capture && Enum.IsDefined(capture)
                ? capture : null
        };
    }

    private ImmutableArray<Contributor> IncludeObservedContributors(ImmutableArray<Contributor> contributors)
    {
        lock (_sync)
        {
            // Each file keeps its own proposal ledger. Run authority is a separate,
            // monotonic union, including callbacks that finish in a different order.
            _observedContributors = _observedContributors.AddRange(contributors).Distinct()
                .OrderBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal).ToImmutableArray();
            return _observedContributors;
        }
    }

    private async Task<ProviderAdmissionStatus> CheckProviderEligibilityAsync(OctoberProviderPhase phase, CancellationToken token)
    {
        if (phase == OctoberProviderPhase.Business)
        {
            if (!BusinessRunAvailable) return IsCancelled ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
            var allowed = await CheckPermissionsAsync(_observedContributors, token);
            if (allowed != DisclosureStatus.Allowed) return allowed == DisclosureStatus.Denied ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.AuthorityUnavailable;
            return BusinessRunAvailable ? ProviderAdmissionStatus.Granted : ProviderAdmissionStatus.HardStopped;
        }
        if (phase != OctoberProviderPhase.Worker) return ProviderAdmissionStatus.Denied;
        if (!FileAvailable) return IsCancelled ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
        if (_activeLedger is null || !await BoundedAsync(_activeLedger.VerifyCurrentAsync(token), token)) return ProviderAdmissionStatus.Denied;
        if (await CheckPermissionsAsync(_observedContributors, token) != DisclosureStatus.Allowed) return ProviderAdmissionStatus.Denied;
        return FileAvailable ? ProviderAdmissionStatus.Granted : IsCancelled ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
    }

    private async Task<DisclosureStatus> CheckPermissionsAsync(ImmutableArray<Contributor> contributors, CancellationToken token)
    {
        if (_execution is null || _disposed) return DisclosureStatus.Denied;
        try
        {
            Task<OctoberAuthoritySelectionResult> selecting = _businessGuard is null
                ? _dependencies.Authority.AuthorizeSelectionAsync(_execution.Session,
                    new(_execution.Authority.TargetId, _execution.Authority.BatchId, null), token)
                : RetainBusinessOwnerCall(() => _dependencies.Authority.AuthorizeSelectionAsync(_execution.Session,
                    new(_execution.Authority.TargetId, _execution.Authority.BatchId, null), token));
            OctoberAuthoritySelectionResult current = await BoundedAsync(selecting, token);
            if (current.Status != OctoberAuthorityDecisionStatus.Authorized || current.Request is null || current.Authority is null ||
                !BindingMatches(current.Request, current.Authority)) return Deny(current.Status == OctoberAuthorityDecisionStatus.Unavailable);
            var allowedAdvisory = _execution.Authority.RepositoryIds.SelectMany(repository => Advisory(repository)).Select(item => item.Contributor).ToHashSet();
            foreach (Contributor contributor in contributors)
            {
                if (contributor.Kind is ContributorKind.Source or ContributorKind.SourceMetadata)
                {
                    var repository = _execution.Setup.Snapshot.Request.Repositories.SingleOrDefault(item => item.RepositoryId == contributor.RepositoryId);
                    if (repository is null || contributor.RootBindingIdentity != repository.RootBindingIdentity ||
                        (contributor.SystemId is not null && contributor.SystemId != repository.SystemId) ||
                        (contributor.Kind == ContributorKind.Source && !OctoberKnowledgeInputPolicy.IsValidSourceContributor(contributor)) ||
                        (contributor.Kind == ContributorKind.SourceMetadata && !OctoberKnowledgeInputPolicy.IsValidMetadataContributor(contributor)))
                        return Deny();
                }
                else if (!allowedAdvisory.Contains(contributor)) return Deny();
            }
            token.ThrowIfCancellationRequested();
            return DisclosureStatus.Allowed;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Deny(unavailable: true); }
    }

    private bool BindingMatches(OctoberSetupRequest request, OctoberAuthorizedSelection authority)
    {
        var execution = _execution!;
        var original = execution.Authority;
        var snapshot = execution.Setup.Snapshot;
        if (authority.SelectedContext is not { } current || original.SelectedContext is not { } selected ||
            snapshot.Authority.SelectedContext is not { } saved ||
            authority.SubjectId != execution.Session.SubjectId || original.SubjectId != execution.Session.SubjectId ||
            current.ManagementAuthorityId != execution.Session.ManagementAuthorityId || selected.ManagementAuthorityId != current.ManagementAuthorityId ||
            authority.TargetId != original.TargetId || authority.BatchId != original.BatchId ||
            authority.CanonicalDatabaseIdentity != original.CanonicalDatabaseIdentity || authority.SelectionDigest != original.SelectionDigest ||
            request.TargetId != original.TargetId || request.BatchId != original.BatchId || request.SelectionDigest != original.SelectionDigest ||
            execution.Setup.TargetId != original.TargetId || execution.Setup.BatchId != original.BatchId ||
            !authority.RepositoryIds.Order().SequenceEqual(original.RepositoryIds.Order()) ||
            !request.Repositories.OrderBy(item => item.RepositoryId).SequenceEqual(snapshot.Request.Repositories.OrderBy(item => item.RepositoryId)) ||
            !current.Repositories.OrderBy(item => item.RepositoryId).SequenceEqual(selected.Repositories.OrderBy(item => item.RepositoryId))) return false;
        return OctoberSelectionDigest.Compute(request, current) == original.SelectionDigest &&
            OctoberSelectionDigest.Compute(snapshot.Request, saved) == original.SelectionDigest &&
            OctoberSelectionDigest.Compute(snapshot.Request, selected) == original.SelectionDigest;
    }

    Task<DisclosureStatus> IOctoberGenerateAdmissionAuthority.CheckAsync(GenerateAdmissionRequest request, CancellationToken token) =>
        request == _beginRequest ? CheckPermissionsAsync([], token) : Task.FromResult(DisclosureStatus.Denied);
    Task<DisclosureStatus> IOctoberKnowledgeProducerAuthorization.CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken token) =>
        binding == _binding ? CheckPermissionsAsync(contributors, token) : Task.FromResult(DisclosureStatus.Denied);
    Task<VerifiedKnowledgeSource?> IOctoberKnowledgeSourceVerifier.VerifyAsync(KnowledgeBinding binding, PreparedOrigin origin, CancellationToken token) =>
        _activeLedger is null ? Task.FromResult<VerifiedKnowledgeSource?>(null) : _activeLedger.VerifyAsync(binding, origin, token);
    Task<DisclosureStatus> IOctoberFileAcceptanceAuthority.VerifyAsync(CompletedFileSubmission submission, CancellationToken token) =>
        _attempts.FirstOrDefault(attempt => attempt.Submission?.Operation == submission.Operation)?.VerifyAsync(submission, token)
        ?? Task.FromResult(DisclosureStatus.Denied);

    public async Task<CorpusFreezeEligibility> VerifyAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default)
    {
        if (_expectedFreeze is null || request.Operation != _expectedFreeze.Operation || request.Binding != _binding ||
            request.CanonicalPayloadDigest != _expectedFreeze.CanonicalPayloadDigest ||
            OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(request) != _expectedFreeze.CanonicalPayloadDigest)
            return CorpusFreezeEligibility.Denied;
        if (!_admissionClosed) return CorpusFreezeEligibility.AdmissionOpen;
        if (!RunAvailable) return CorpusFreezeEligibility.HardStopped;
        if (Unresolved() || _scope?.GetSnapshot().OutstandingAttempts != 0 || _attempts.Any(attempt => !attempt.IsSettledForFreeze))
            return CorpusFreezeEligibility.Unsettled;
        var allowed = await CheckPermissionsAsync(_observedContributors, cancellationToken);
        if (!RunAvailable) return CorpusFreezeEligibility.HardStopped;
        return allowed == DisclosureStatus.Allowed ? CorpusFreezeEligibility.Eligible :
            allowed == DisclosureStatus.Denied ? CorpusFreezeEligibility.Denied : CorpusFreezeEligibility.AuthorityUnavailable;
    }

    public async Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default)
    {
        var status = BeginEligibility(attempt, cancellationToken);
        if (status != CommitEligibilityStatus.Eligible) return status;
        var authority = await CheckPermissionsAsync([], cancellationToken);
        status = BeginEligibility(attempt, cancellationToken);
        return status != CommitEligibilityStatus.Eligible ? status : authority == DisclosureStatus.Allowed
            ? CommitEligibilityStatus.Eligible : authority == DisclosureStatus.Denied ? CommitEligibilityStatus.Denied : CommitEligibilityStatus.AuthorityUnavailable;
    }

    public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync,
        CancellationToken cancellationToken = default)
    {
        bool held = false;
        CancellationTokenSource? commitToken = null;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _runToken!.Token);
            await _beginGate.WaitAsync(linked.Token);
            held = true;
            var eligible = await CheckBeforeTransactionAsync(attempt, linked.Token);
            if (eligible != CommitEligibilityStatus.Eligible) return BeginDenied(eligible);
            commitToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _runToken.Token);
            Task<CommitSettlement> actual;
            lock (_sync)
            {
                eligible = BeginEligibility(attempt, cancellationToken);
                if (eligible != CommitEligibilityStatus.Eligible) return BeginDenied(eligible);
                if (_beginCommit is not null) actual = _beginCommit;
                else
                {
                    _beginUncertain = true;
                    _beginSettlement = CommitSettlement.Indeterminate;
                    SubmittedCommit submitted = submitAsync(commitToken.Token);
                    if (submitted?.Settlement is null) return new(CommitSubmissionStatus.AuthorityUnavailable, CommitSettlement.Indeterminate);
                    _beginSubmitted = true;
                    _beginUncertain = false;
                    actual = ObserveBeginCommitAsync(submitted.Settlement, commitToken);
                    _beginCommit = actual;
                    commitToken = null;
                }
            }
            _beginGate.Release(); held = false;
            try { return new(CommitSubmissionStatus.Submitted, await actual.WaitAsync(linked.Token)); }
            catch (OperationCanceledException) { return new(CommitSubmissionStatus.Submitted, CommitSettlement.Indeterminate); }
        }
        catch (OperationCanceledException) { return BeginDenied(BeginEligibility(attempt, cancellationToken)); }
        catch (Exception) { return new(_beginSubmitted ? CommitSubmissionStatus.Submitted : CommitSubmissionStatus.AuthorityUnavailable, _beginUncertain ? CommitSettlement.Indeterminate : null); }
        finally { commitToken?.Dispose(); if (held) _beginGate.Release(); }
    }

    private async Task<CommitSettlement> ObserveBeginCommitAsync(Task<CommitSettlement> task, CancellationTokenSource token)
    {
        try
        {
            CommitSettlement settled;
            try { settled = await task; } catch (Exception) { settled = CommitSettlement.Indeterminate; }
            lock (_sync) if (_beginSettlement != CommitSettlement.Committed) _beginSettlement = settled;
            return settled;
        }
        finally { token.Dispose(); }
    }

    private CommitEligibilityStatus BeginEligibility(CommitAttempt attempt, CancellationToken token)
    {
        lock (_sync)
        {
            if (!RunAvailable || _beginClosed) return CommitEligibilityStatus.StaleOrFenced;
            if (token.IsCancellationRequested) return CommitEligibilityStatus.Cancelled;
            return _beginRequest is not null && attempt.Operation == OctoberGenerateAdmissionPolicy.OperationFor(_beginRequest.Binding) &&
                attempt.AttemptId == _beginRequest.RunId && attempt.PayloadDigest == _beginRequest.CanonicalPayloadDigest
                ? CommitEligibilityStatus.Eligible : CommitEligibilityStatus.Denied;
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync) { if (_disposed) return; _fenced = true; _beginClosed = true; }
        _stop.Cancel();
        if (_executionTask is { IsCompleted: false }) throw new InvalidOperationException("october_run_settlement_unconfirmed");
        await SettleResourcesAsync();
        lock (_sync) _disposed = true;
        _runToken?.Dispose(); _deadline?.Dispose(); _stop.Dispose(); _beginGate.Dispose();
    }

    private async Task SettleResourcesAsync()
    {
        _dependencies.HttpObservation?.Discard();
        await AwaitOriginalBusinessSettlementAsync();
        await AwaitBusinessOwnerCallsAsync();
        if (_businessGuard is not null) await _businessGuard.ObservePendingAsync();
        if (_resourcesSettled) return;
        if (Unresolved() || HasOutstandingCalls())
            throw new InvalidOperationException("october_run_settlement_unconfirmed");
        await DisposeBusinessReadersAsync();
        foreach (OctoberAttemptState attempt in _attempts) await attempt.DisposeAsync();
        if (_publication is not null) await _publication.DisposeAsync();
        if (_businessGuard is not null) await _businessGuard.DisposeAsync();
        await CloseAnalysisResourcesAsync();
        // The exchange may contain preserved foreign entries or locked owned
        // metadata. Other settled resources can close, but that failure cannot
        // become a successful run settlement on this or a repeated disposal.
        if (_operatorCleanupFailure is not null) throw _operatorCleanupFailure;
        _resourcesSettled = true;
        _cleanupFailure = null;
    }

    private async Task CloseAnalysisResourcesAsync()
    {
        await AwaitOriginalBusinessSettlementAsync();
        if (_analysisCleanup is not null) { await _analysisCleanup; return; }
        if (Unresolved() || HasOutstandingCalls())
            throw new InvalidOperationException("october_run_settlement_unconfirmed");
        _analysisCleanup = CloseAnalysisCoreAsync();
        await _analysisCleanup;
    }

    private async Task CloseAnalysisCoreAsync()
    {
        foreach (IChatClient client in _clients) client.Dispose();
        _clients.Clear();
        await CloseSourceResourcesAsync();
        if (_scope is not null) await _scope.DisposeAsync();
    }

    private bool HasOutstandingCalls()
    {
        lock (_sync) return _outstanding.Any(task => !task.IsCompleted);
    }

    private async Task AcquireSourceAsync(OctoberGenerateExecution execution, CancellationToken token)
    {
        long started = _time.GetTimestamp();
        Task<IOctoberScanRunSource> acquisition = _dependencies.OpenSource(execution, RemainingRun, token);
        lock (_sync) _outstanding.Add(acquisition);
        try
        {
            // An uncooperative factory still owns its eventual result. Await it,
            // adopt that resource, then honor cancellation before using it.
            _source = await acquisition ?? throw new InvalidOperationException("Source acquisition returned no owner.");
            token.ThrowIfCancellationRequested();
            if (!RunAvailable) throw new OperationCanceledException("The original run is no longer eligible.");
        }
        finally
        {
            Observe(acquisition);
            lock (_sync) _toolWait += _time.GetElapsedTime(started);
        }
    }

    private async Task<IChatClient> AcquireClientAsync(OctoberSelectedWorkerProvider provider, CancellationToken token)
    {
        Task<IChatClient> acquisition = _dependencies.CreateWorkerClient(provider, token);
        lock (_sync) _outstanding.Add(acquisition);
        try
        {
            IChatClient client = await acquisition ?? throw new InvalidOperationException("Client acquisition returned no owner.");
            _clients.Add(client);
            token.ThrowIfCancellationRequested();
            if (!FileAvailable) throw new OperationCanceledException("The original file is no longer eligible.");
            return client;
        }
        finally { Observe(acquisition); }
    }

    private bool Unresolved() => _businessGuard?.HasUnsettledWrites == true || _beginUncertain || _beginCommit is { IsCompleted: false } ||
        _beginSettlement == CommitSettlement.Indeterminate || _attempts.Any(attempt =>
            attempt.HasSubmitted && !attempt.IsSettledForFreeze) || _scope?.GetSnapshot().HasUnconfirmedSettlement == true;
    private TimeSpan RemainingRun => TimeSpan.FromSeconds(_execution!.Policy.RunDeadlineSeconds) - _time.GetElapsedTime(_started);
    private TimeSpan RemainingFile => Min(
        _fileStarted is { } started
            ? Min(RemainingRun, TimeSpan.FromSeconds(_execution!.Policy.FileDeadlineSeconds) - _time.GetElapsedTime(started)) : RemainingRun,
        _sourceWindowStarted is { } windowStarted ? _sourceWindow - _time.GetElapsedTime(windowStarted) : RemainingRun);
    private bool RunAvailable => !_fenced && !_disposed && !IsCancelled && RemainingRun > TimeSpan.Zero;
    private bool FileAvailable => RunAvailable && RemainingFile > TimeSpan.Zero;
    private bool IsCancelled => _caller.IsCancellationRequested || _stop.IsCancellationRequested;
    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;
    private DisclosureStatus Deny(bool unavailable = false) { lock (_sync) _fenced = true; return unavailable ? DisclosureStatus.AuthorityUnavailable : DisclosureStatus.Denied; }

    private async Task<DisclosureStatus> DisclosureAsync(CancellationToken token)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        try { return await CheckPermissionsAsync(_observedContributors, linked.Token); }
        catch (Exception) { return Deny(unavailable: true); }
    }

    private async Task<T> BoundedAsync<T>(Task<T> task, CancellationToken token)
    {
        lock (_sync) _outstanding.Add(task);
        try { return await task.WaitAsync(token); }
        finally
        {
            if (task.IsCompleted) Observe(task);
            else _ = task.ContinueWith(Observe, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
    private void Observe(Task task) { if (task.IsFaulted) _ = task.Exception; lock (_sync) _outstanding.Remove(task); }
    private async Task<T> ToolAsync<T>(Func<Task<T>> action, CancellationToken token)
    {
        long started = _time.GetTimestamp();
        try { return await BoundedAsync(action(), token); }
        finally { lock (_sync) _toolWait += _time.GetElapsedTime(started); }
    }
    private async Task<T> StoreAsync<T>(Func<Task<T>> action, CancellationToken token)
    {
        long started = _time.GetTimestamp();
        try { return await BoundedAsync(action(), token); }
        finally { lock (_sync) _storeWait += _time.GetElapsedTime(started); }
    }

    private OctoberScanReport Failure(string code, bool interrupted = false, bool indeterminate = false, bool disclose = true, string? stage = null)
    {
        return new(stage ?? _execution?.Policy.OutputStage ?? string.Empty, null,
            disclose && !_inventory.IsEmpty ? OctoberCoverageStatus.Partial : OctoberCoverageStatus.None,
            indeterminate ? OctoberExecutionStatus.Indeterminate : interrupted ? OctoberExecutionStatus.Interrupted : OctoberExecutionStatus.Failed,
            indeterminate ? OctoberPublicationStatus.Indeterminate : OctoberPublicationStatus.NotRequested,
            null, [], disclose ? _accepted : [], disclose ? _coverage : [],
            new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new(0, 0, 0, false), disclose ? _gaps.AddRange(_unstartedFiles.Select(file =>
                new OctoberScanGap("selected-file-unprocessed", file.RepositoryId, file.RelativePath))).Add(new(code)) : [new(code)])
        {
            Inventory = disclose ? _inventory : [],
            Matching = FailurePhase(_matchingPhase, stage, interrupted, indeterminate, disclose),
            BusinessLogic = FailurePhase(_businessLogicPhase, stage, interrupted, indeterminate, disclose)
        };
    }
    private OctoberPhaseStatus FailurePhase(OctoberPhaseStatus phase, string? stage, bool interrupted, bool indeterminate, bool disclose) =>
        (stage ?? _execution?.Policy.OutputStage) != "cl-and-bl" ? OctoberPhaseStatus.NotRequested :
        !disclose || _fenced ? OctoberPhaseStatus.Unavailable : phase == OctoberPhaseStatus.Indeterminate && !indeterminate
            ? interrupted ? OctoberPhaseStatus.Interrupted : OctoberPhaseStatus.Failed : phase;
    private static OctoberScanReport Hide(OctoberScanReport report, string code) => report with
    {
        CompletedStage = null, Coverage = OctoberCoverageStatus.None, Execution = OctoberExecutionStatus.Failed,
        PublicationStatus = OctoberPublicationStatus.Failed, Publication = null, Documents = [], AcceptedFiles = [],
        TerminalCoverage = [], Inventory = [], Gaps = [new(code)],
        BusinessDiagnostics = null,
        WorkerDiagnostics = null,
        HardRetry = null,
        Matching = report.RequestedStage == "cl-and-bl" ? OctoberPhaseStatus.Unavailable : OctoberPhaseStatus.NotRequested,
        BusinessLogic = report.RequestedStage == "cl-and-bl" ? OctoberPhaseStatus.Unavailable : OctoberPhaseStatus.NotRequested
    };
    private OctoberScanReport MergeMetrics(OctoberScanReport report)
    {
        var snapshot = _scope?.GetSnapshot();
        return report with
        {
            Usage = snapshot?.Usage ?? report.Usage,
            Timings = new(_time.GetElapsedTime(_started), snapshot?.QueueWait ?? TimeSpan.Zero,
                _toolWait, snapshot?.ModelWait ?? TimeSpan.Zero, _storeWait + report.Timings.StoreWait)
        };
    }
    private static bool HasMetadata(OctoberScanReport report) => report.HardRetry is not null || report.WorkerDiagnostics is not null || report.BusinessDiagnostics is not null || report.Publication is not null ||
        !report.Documents.IsEmpty || !report.AcceptedFiles.IsEmpty || !report.TerminalCoverage.IsEmpty || !report.Inventory.IsEmpty;
    private static OctoberGeneratorExecutionResult Finished(OctoberScanReport report) => new(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report);
    private static CommitGuardResult BeginDenied(CommitEligibilityStatus status) => new(status switch
    {
        CommitEligibilityStatus.Denied => CommitSubmissionStatus.Denied,
        CommitEligibilityStatus.Cancelled => CommitSubmissionStatus.CancelledBeforeSubmission,
        CommitEligibilityStatus.AuthorityUnavailable => CommitSubmissionStatus.AuthorityUnavailable,
        _ => CommitSubmissionStatus.StaleOrFenced
    }, null);

    private sealed class MeasuredTool(OctoberGenerateRunner owner, AIFunction tool) : AIFunction
    {
        public override string Name => tool.Name;
        public override string Description => tool.Description;
        public override JsonElement JsonSchema => tool.JsonSchema;
        public override JsonElement? ReturnJsonSchema => tool.ReturnJsonSchema;
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) =>
            await owner.ToolAsync(() => tool.InvokeAsync(arguments, cancellationToken).AsTask(), cancellationToken);
    }
}
