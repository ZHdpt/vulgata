using System.Collections.Immutable;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>Trusted command composition over the existing Business owner services and verified reader binding.</summary>
internal sealed record OctoberBusinessRunComposition(
    Func<IOctoberKnowledgeProducerAuthorization, IOctoberCommitSubmissionGuard, IOctoberResolutionStore> CreateResolutionStore,
    Func<IOctoberKnowledgeProducerAuthorization, IOctoberFlowPlanningInputLoader> CreatePlanningInputLoader,
    Func<IOctoberKnowledgeProducerAuthorization, IOctoberCommitSubmissionGuard, IOctoberFlowPublicationStore> CreatePublicationStore,
    Func<FlowPlanningInput, ImmutableArray<FlowRootSelection>> SelectRoots,
    BusinessProviderConfigurationProjection ProviderConfiguration,
    string ResolutionInstructions,
    string SynthesisInstructions,
    FlowPlanningLimits Limits,
    long MaximumAttemptTokens)
{
    internal BusinessAdvisoryContextOverride? ContextOverride { get; init; }
    internal Func<IOctoberOperatorSession, PublicationBinding, CancellationToken, Task<OctoberVerifiedReaderBinding?>>? BindReader { get; init; }
    internal OctoberOperatorRootSelectionOptions? OperatorSelection { get; init; }
    internal Func<OctoberOperatorRootSelectionRequest, CancellationToken, Task<OctoberOperatorRootSelectionResponse?>>? InteractAsync { get; init; }
}

internal sealed partial class OctoberGenerateRunner
{
    private OperationKey? _businessFinalizationOperation;
    private IChatClient? _businessClient;
    private BusinessCommitGuard? _businessGuard;
    private Task? _sourceCleanup;
    private bool _businessPublicationLogUnavailable;
    private bool _businessResolutionFailed;
    private bool _businessResolutionLimited;
    private OctoberPhaseStatus _matchingPhase = OctoberPhaseStatus.NotStarted;
    private OctoberPhaseStatus _businessLogicPhase = OctoberPhaseStatus.NotStarted;
    private readonly HashSet<Task> _businessOwnerCalls = [];
    private readonly List<IAsyncDisposable> _businessReaderLifetimes = [];
    private Task? _businessReaderCleanup;
    private OctoberOperatorCleanupUnconfirmedException? _operatorCleanupFailure;
    private OctoberBusinessDiagnosticSnapshot? _businessDiagnostics;
    private OctoberBusinessRunComposition? _businessContextComposition;
    private BusinessAdvisoryContextOverride? _businessContextOverride;
    private CorpusHandle? _businessContextCorpus;
    private string? _businessContextOverrideDigest;
    private ImmutableArray<Contributor> _businessAdditionalAdvisory = [];

    private async Task<ImmutableArray<FlowRootSelection>> SelectBusinessRootsAsync(FlowPlanningInput input,
        CancellationToken cancellationToken)
    {
        var business = _dependencies.Business!;
        var options = business.OperatorSelection;
        if (options is null || options.Mode == "exact-settings") return business.SelectRoots(input);
        if (options.Mode != "operator" || options.TimeoutSeconds is not (>= 1 and <= 300) ||
            business.InteractAsync is null || input.Units.IsDefault || input.Units.Length > 1024 ||
            input.Units.Select(unit => unit.UnitId).Distinct().Count() != input.Units.Length)
            return default;
        var candidates = input.Units.Select(unit => new OctoberOperatorRootCandidate(unit.UnitId, unit.Document,
            unit.PrimaryOrigin.RepositoryId, unit.PrimaryOrigin.RelativePath, unit.SemanticSelector)).ToImmutableArray();
        if (candidates.Any(candidate => candidate.UnitId == Guid.Empty || candidate.Document is null ||
            candidate.Document.BatchId != _binding!.BatchId || candidate.Document.LogicalDocumentId == Guid.Empty ||
            candidate.Document.DocumentVersionId == Guid.Empty ||
            !_execution!.Authority.RepositoryIds.Contains(candidate.RepositoryId) ||
            !new OctoberBusinessSettings([new(candidate.RepositoryId, candidate.RelativePath, candidate.SemanticSelector,
                FlowRootKind.SelectedCodeUnit)], business.Limits, business.MaximumAttemptTokens).IsValid()))
            return default;
        if (!OperatorSelectionEligible || await CheckPermissionsAsync(_observedContributors, cancellationToken) != DisclosureStatus.Allowed || !OperatorSelectionEligible)
            return default;
        TimeSpan remaining = Min(TimeSpan.FromSeconds(options.TimeoutSeconds.Value),
            Min(RemainingRun, _execution!.Session.ExpiresAtUtc - _time.GetUtcNow()));
        if (remaining <= TimeSpan.Zero) return default;
        long started = _time.GetTimestamp();
        var request = new OctoberOperatorRootSelectionRequest(1, Guid.NewGuid(), OriginalRunId,
            input.Corpus.Handle.CorpusId, _time.GetUtcNow() + remaining, candidates);
        if (System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)).Length > 1024 * 1024)
            return default;
        using var deadline = new CancellationTokenSource(remaining, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        OctoberOperatorRootSelectionResponse? response;
        try
        {
            // A timed-out waiter does not release the actual interaction or its
            // owned files. The original run retains and drains that call.
            response = await BoundedAsync(RetainBusinessOwnerCall(async () =>
            {
                try { return await business.InteractAsync(request, linked.Token); }
                catch (OctoberOperatorCleanupUnconfirmedException error)
                {
                    // Record actual cleanup failure even if the bounded waiter
                    // already stopped. It remains owned by this original run.
                    lock (_sync) _operatorCleanupFailure ??= error;
                    throw;
                }
            }), linked.Token);
        }
        finally { await AwaitBusinessOwnerCallsAsync(); }
        if (_time.GetElapsedTime(started) >= remaining || linked.IsCancellationRequested || !OperatorSelectionEligible ||
            await CheckPermissionsAsync(_observedContributors, linked.Token) != DisclosureStatus.Allowed ||
            _time.GetElapsedTime(started) >= remaining || !OperatorSelectionEligible || response is null ||
            response.Version != 1 || response.RequestId != request.RequestId || response.RunId != request.RunId ||
            response.SelectedUnitIds.IsDefault || response.SelectedUnitIds.Length > 256 ||
            response.SelectedUnitIds.Distinct().Count() != response.SelectedUnitIds.Length)
            return default;
        var byId = candidates.ToDictionary(candidate => candidate.UnitId);
        if (response.SelectedUnitIds.Any(id => !byId.ContainsKey(id))) return default;
        return response.SelectedUnitIds.Select(id =>
        {
            var candidate = byId[id];
            return new FlowRootSelection(candidate.UnitId, candidate.Document, FlowRootKind.SelectedCodeUnit, candidate.SemanticSelector);
        }).ToImmutableArray();
    }

    private bool OperatorSelectionEligible => BusinessRunAvailable && (_beginRequest is not null || _existingClRequest is not null) &&
        _businessFinalizationOperation is { } operation && _scope?.CheckHandoffEligibility(OriginalRunId,
            operation, _execution!.Provider.Profile.Name) == ProviderAdmissionStatus.Granted;

    private bool BusinessRunAvailable => _execution is not null && RunAvailable &&
        _execution.Session.ExpiresAtUtc > _time.GetUtcNow() && _runToken is { IsCancellationRequested: false };

    private bool BusinessConfigurationMatches(OctoberGenerateExecution execution)
    {
        if (execution.Policy.OutputStage != "cl-and-bl" || _dependencies.Business is not { } business ||
            business.CreateResolutionStore is null || business.CreatePlanningInputLoader is null ||
            business.CreatePublicationStore is null || business.BindReader is null || business.SelectRoots is null ||
            business.ProviderConfiguration is not { } configuration || business.Limits is not { } limits ||
            string.IsNullOrWhiteSpace(business.ResolutionInstructions) || string.IsNullOrWhiteSpace(business.SynthesisInstructions) ||
            business.ResolutionInstructions.Length > KnowledgeContract.MaximumContentBytes ||
            business.SynthesisInstructions.Length > KnowledgeContract.MaximumContentBytes ||
            business.MaximumAttemptTokens <= 0 || business.MaximumAttemptTokens > execution.Policy.MaximumTotalTokens ||
            business.ContextOverride is not null && (execution.Policy.ExistingClPublicationId is null ||
                !OctoberBusinessContextOverridePolicy.ValidateShape(business.ContextOverride)) ||
            limits.MaxNodes is < 1 or > OctoberFlowPlanner.MaximumNodes || limits.MaxEdges is < 0 or > OctoberFlowPlanner.MaximumEdges ||
            limits.MaxDepth is < 0 or > OctoberFlowPlanner.MaximumDepth)
            return false;
        var selected = execution.Provider;
        var profile = selected.Profile;
        // One explicit provider document is the existing host precedence. These
        // are the concurred canonical wire spellings, not a profile fallback.
        return selected.Precedence == "only-source" && configuration.Precedence == 0 &&
            configuration.SourceIdentity == selected.SourceIdentity && configuration.ProfileName == profile.Name &&
            profile.ApiType.ToString() == "ChatCompletions" && configuration.ApiType == "chat-completions" &&
            configuration.Endpoint == profile.Endpoint && configuration.Model == profile.Model &&
            configuration.CredentialReferenceId == profile.CredentialReference;
    }

    private ImmutableArray<Contributor> CurrentBusinessAdditionalAdvisory()
    {
        if (_businessContextOverride is null) return [];
        // This is retained disclosure authority, not a new dispatch grant. Original scope/guard
        // eligibility still gates semantic work; completed-result checks may follow resource settlement.
        if (_execution is null || _businessGuard is null || _existingClSnapshot is not { } existing ||
            _execution!.Policy.ExistingClPublicationId != existing.Publication.PublicationId ||
            _businessContextCorpus != _confirmedBusinessCorpus || _businessContextCorpus != existing.Corpus ||
            !ReferenceEquals(_dependencies.Business, _businessContextComposition) ||
            !ReferenceEquals(_businessContextComposition?.ContextOverride, _businessContextOverride) ||
            OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(_businessContextOverride)) != _businessContextOverrideDigest ||
            _businessAdditionalAdvisory.IsDefault || _businessAdditionalAdvisory.Any(contributor =>
                contributor.Kind is not (ContributorKind.SystemAdvisory or ContributorKind.RepositoryAdvisory)))
            return default;
        if (_originalBusinessRequest is { } original && (original.Selection?.ContextOverride is not { } selected ||
            OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(selected)) != _businessContextOverrideDigest))
            return default;
        return _businessAdditionalAdvisory;
    }

    private Task CloseSourceResourcesAsync() => _sourceCleanup ??= _source is null
        ? Task.CompletedTask : _source.DisposeAsync().AsTask();

    private OctoberScanReport BusinessFailure(string code, bool interrupted = false, bool indeterminate = false, bool disclose = true) =>
        Failure(code, interrupted, indeterminate, disclose) with { BusinessDiagnostics = _fenced ? null : _businessDiagnostics };

    private async Task<OctoberScanReport> RunBusinessCompletionAsync(CancellationToken cancellationToken)
    {
        var business = _dependencies.Business!;
        var execution = _execution!;
        var binding = _binding!;
        if (!BusinessRunAvailable || await CheckPermissionsAsync(_observedContributors, cancellationToken) != DisclosureStatus.Allowed)
            return BusinessFailure("business_authority_unavailable", interrupted: IsCancelled, disclose: false);
        _businessGuard = new(this);
        CorpusHandle corpus;
        if (_existingClSnapshot is { } existing) corpus = existing.Corpus;
        else
        {
            var freeze = new CorpusFreezeRequest(new(binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()), binding, "", _accepted, _coverage);
            _expectedFreeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
            _businessGuard.Register(new(_expectedFreeze.Operation, _expectedFreeze.Operation.OperationId, _expectedFreeze.CanonicalPayloadDigest));
            var writer = _dependencies.CreateKnowledgeWriter(new(this, _businessGuard, this, this, this));
            KnowledgeWriteResult frozen;
            try { frozen = await StoreAsync(() => RetainBusinessOwnerCall(() => writer.FreezeCorpusAsync(_expectedFreeze, cancellationToken)), cancellationToken); }
            catch (Exception) { frozen = new(KnowledgeWriteStatus.CommitIndeterminate, _expectedFreeze.Operation); }
            if (frozen.Status == KnowledgeWriteStatus.CommitIndeterminate && BusinessRunAvailable)
                frozen = await StoreAsync(() => RetainBusinessOwnerCall(() => writer.LookupOperationAsync(binding, _expectedFreeze.Operation,
                    _expectedFreeze.CanonicalPayloadDigest, cancellationToken)), cancellationToken);
            if (frozen.Operation != _expectedFreeze.Operation || !KnownCommitted(frozen.Status) ||
                frozen.Corpus is not { } frozenCorpus || frozenCorpus.Binding != binding || frozenCorpus.CorpusId == Guid.Empty ||
                frozenCorpus.CorpusDigest != _expectedFreeze.CanonicalPayloadDigest)
                return BusinessFailure("business_freeze_unavailable", indeterminate: frozen.Status == KnowledgeWriteStatus.CommitIndeterminate,
                    interrupted: IsCancelled, disclose: false);
            _businessGuard.ConfirmCommitted(_expectedFreeze.Operation);
            corpus = frozenCorpus;
            if (frozen.Status == KnowledgeWriteStatus.LogUnavailable) return BusinessFailure("operation_log_unavailable");
        }
        _confirmedBusinessCorpus = corpus;

        if (!BusinessRunAvailable) return BusinessFailure("business_run_ineligible", interrupted: true);

        _businessClient ??= await AcquireClientAsync(execution.Provider, cancellationToken);
        if (_businessClient is not IOctoberBusinessAttemptClient bound || bound.ProviderProfileId != execution.Provider.Profile.Name)
            return BusinessFailure("business_client_unavailable");
        var resolutionStore = new ObservedBusinessResolutionStore(this, business.CreateResolutionStore(this, _businessGuard));
        var disclosure = new BusinessDisclosure(this);
        _matchingPhase = OctoberPhaseStatus.Indeterminate;
        var loaded = await StoreAsync(() => resolutionStore.LoadCorpusAsync(corpus, cancellationToken), cancellationToken);
        if (loaded.Status != KnowledgeReadStatus.Success || loaded.Corpus is not { } acceptedCorpus || acceptedCorpus.Handle != corpus)
            return BusinessFailure("business_corpus_unavailable", disclose: false);
        var resolutionPlan = OctoberResolutionPolicy.Plan(acceptedCorpus);
        var resolver = new OctoberResolutionExecutor(_businessClient, business.ResolutionInstructions, _time);
        foreach (var inquiry in resolutionPlan.Inquiries)
        {
            if (!BusinessRunAvailable) return BusinessFailure("business_run_ineligible", interrupted: true);
            if (_existingClSnapshot is { } snapshot)
            {
                var retained = snapshot.ExistingResolutions.SingleOrDefault(item => item.Fingerprint == inquiry.Fingerprint);
                if (retained is not null)
                {
                    if (retained.Kind is ResolutionOutcomeKind.Failed or ResolutionOutcomeKind.NotEvaluated)
                        return BusinessFailure("business_existing_resolution_unavailable");
                    IncludeObservedContributors(retained.Contributors);
                    continue;
                }
            }
            var resolved = await resolver.ExecuteAsync(acceptedCorpus, inquiry, _businessFinalizationOperation!,
                OriginalRunId, execution.Provider.Profile.Name, business.MaximumAttemptTokens, _scope!, disclosure, cancellationToken);
            if (resolved.AdmissionFailure is ProviderAdmissionStatus.Denied or ProviderAdmissionStatus.AuthorityUnavailable or ProviderAdmissionStatus.HardStopped)
                return BusinessFailure("business_resolution_ineligible", interrupted: IsCancelled, disclose: false);
            if (_existingClSnapshot is not null && resolved.Witness is null)
            {
                _matchingPhase = OctoberPhaseStatus.Failed;
                return BusinessFailure("business_resolution_failed");
            }
            bool notEvaluatedForLimit = resolved.Witness is null &&
                resolved.AdmissionFailure == ProviderAdmissionStatus.BudgetExhausted && resolved.Attempts.Length == 0;
            var acceptance = new ResolutionAcceptanceRequest(new(binding.BatchId, OperationKind.AcceptResolution, Guid.NewGuid()),
                corpus, Guid.NewGuid(), "", inquiry.Fingerprint, resolved.Witness,
                resolved.Witness is null ? notEvaluatedForLimit ? ResolutionOutcomeKind.NotEvaluated : ResolutionOutcomeKind.Failed : null);
            acceptance = acceptance with { CanonicalPayloadDigest = OctoberResolutionStore.ComputeAcceptanceDigest(acceptance) };
            _businessGuard.Register(new(acceptance.Operation, acceptance.AttemptId, acceptance.CanonicalPayloadDigest));
            ResolutionAcceptanceResult saved;
            try { saved = await StoreAsync(() => resolutionStore.AcceptOutcomeAsync(acceptance, cancellationToken), cancellationToken); }
            catch (Exception) { saved = new(KnowledgeWriteStatus.CommitIndeterminate, acceptance.Operation, null); }
            if (saved.Status == KnowledgeWriteStatus.CommitIndeterminate && BusinessRunAvailable)
                saved = await StoreAsync(() => resolutionStore.LookupOutcomeAsync(corpus, acceptance.Operation,
                    acceptance.CanonicalPayloadDigest, cancellationToken), cancellationToken);
            if (!KnownCommitted(saved.Status) || saved.Operation != acceptance.Operation || saved.Outcome?.Fingerprint != inquiry.Fingerprint)
                return BusinessFailure("business_resolution_unavailable", indeterminate: saved.Status == KnowledgeWriteStatus.CommitIndeterminate, disclose: false);
            _businessGuard.ConfirmCommitted(acceptance.Operation);
            if (saved.Outcome.Kind == ResolutionOutcomeKind.Failed)
            {
                _businessResolutionFailed = true;
                _matchingPhase = OctoberPhaseStatus.Failed;
                if (!_gaps.Any(gap => gap.Code == "business_resolution_failed"))
                    _gaps = _gaps.Add(new("business_resolution_failed"));
            }
            if (notEvaluatedForLimit && saved.Outcome.Kind == ResolutionOutcomeKind.NotEvaluated)
            {
                _businessResolutionLimited = true;
                if (!_businessResolutionFailed) _matchingPhase = OctoberPhaseStatus.Partial;
                if (!_gaps.Any(gap => gap.Code == "business_resolution_limit"))
                    _gaps = _gaps.Add(new("business_resolution_limit"));
            }
            if (saved.Status == KnowledgeWriteStatus.LogUnavailable) return BusinessFailure("operation_log_unavailable");
        }
        var loader = new ObservedBusinessPlanningLoader(this, business.CreatePlanningInputLoader(this));
        var planning = await StoreAsync(() => loader.LoadAsync(corpus, [], cancellationToken), cancellationToken);
        if (planning.Status != KnowledgeReadStatus.Success || planning.Input is not { } input || input.Corpus.Handle != corpus || input.Contributors.IsDefault)
            return BusinessFailure("business_planning_inputs_unavailable", disclose: false);
        if (!_businessResolutionFailed && !_businessResolutionLimited) _matchingPhase = OctoberPhaseStatus.Completed;
        // The retained corpus/B1 ledger stays untouched. Only the owner projection supplies
        // new advisory versions, and only this existing-CL Business composition can authorize them.
        var effectiveInput = input;
        var effectiveContributors = input.Contributors;
        if (business.ContextOverride is { } contextOverride)
        {
            if (_existingClSnapshot is null || execution.Policy.ExistingClPublicationId is null)
                return BusinessFailure("business_context_override_unavailable", disclose: false);
            try
            {
                effectiveInput = OctoberBusinessContextOverridePolicy.Apply(input, contextOverride);
                effectiveContributors = OctoberBusinessContextOverridePolicy.EffectiveContributors(effectiveInput);
                _businessContextComposition = business;
                _businessContextOverride = contextOverride;
                _businessContextCorpus = corpus;
                _businessContextOverrideDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(contextOverride));
                _businessAdditionalAdvisory = effectiveInput.AdditionalAdvisoryContributors;
            }
            catch (ArgumentException)
            {
                return BusinessFailure("business_context_override_unavailable", disclose: false);
            }
        }
        var allObserved = IncludeObservedContributors(effectiveContributors);
        if (!BusinessRunAvailable || await CheckPermissionsAsync(allObserved, cancellationToken) != DisclosureStatus.Allowed)
            return BusinessFailure("business_authority_unavailable", interrupted: IsCancelled, disclose: false);
        var roots = await SelectBusinessRootsAsync(effectiveInput, cancellationToken);
        if (roots.IsDefault) return BusinessFailure("business_roots_unavailable");
        var selection = new BusinessFinalizationSelection(roots, business.Limits, OriginalRunId,
            execution.Provider.Profile.Name, OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(business.ProviderConfiguration),
            OctoberKnowledgePolicy.Hash(business.SynthesisInstructions), business.MaximumAttemptTokens)
        { ContextOverride = _businessContextOverride };
        var request = new FinalizationRequest(_businessFinalizationOperation!, corpus, "", _scope!) { Selection = selection };
        request = request with { CanonicalPayloadDigest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(request) };
        _originalBusinessRequest = request;
        _businessProviderConfiguration = business.ProviderConfiguration;
        _businessDiagnostics = new(null, null, null, null, OctoberBusinessDiagnosticBoundary.OriginalSlot, false);
        var slot = TryIssueOriginalBusinessFinalizationSlot();
        if (slot is null) return BusinessFailure("business_original_slot_unavailable", interrupted: IsCancelled, disclose: false);
        var ownerPublications = business.CreatePublicationStore(this, _businessGuard);
        if (ownerPublications is not IOctoberFlowPublicationInitialRead initialRead) return BusinessFailure("business_initial_read_unavailable");
        var publications = new ObservedBusinessPublicationStore(this, ownerPublications, initialRead);
        _businessGuard.Register(new(request.Operation, request.Operation.OperationId, request.CanonicalPayloadDigest));
        var finalizer = new OctoberBusinessFinalizer(slot, publications, loader, _businessClient,
            execution.Provider.Profile.Name, business.ProviderConfiguration, business.SynthesisInstructions, disclosure, _time);
        _businessLogicPhase = OctoberPhaseStatus.Indeterminate;
        _businessDiagnostics = _businessDiagnostics with { Boundary = OctoberBusinessDiagnosticBoundary.Finalizer };
        try
        {
            var immediate = await finalizer.FinalizeAsync(request, cancellationToken);
            ObserveBusinessOutcome(request, immediate, finalizer: true);
        }
        finally { await AwaitBusinessOwnerCallsAsync(); }
        // Bounded orchestration may finish before an actual owner call returns.
        // Its late result is settlement evidence, never fresh disclosure authority.
        if (BusinessHandoffFailure(request) is { } beforeLookup) return beforeLookup;
        BusinessFinalizationResult outcome;
        _businessDiagnostics = _businessDiagnostics with { Boundary = OctoberBusinessDiagnosticBoundary.ExactLookup };
        try
        {
            outcome = await StoreAsync(() => publications.LookupAsync(corpus, request.Operation,
                request.CanonicalPayloadDigest, cancellationToken), cancellationToken);
            ObserveBusinessOutcome(request, outcome, finalizer: false);
        }
        finally { await AwaitBusinessOwnerCallsAsync(); }
        if (BusinessHandoffFailure(request) is { } afterLookup) return afterLookup;
        bool completed = outcome.Status is BusinessFinalizationStatus.Published or BusinessFinalizationStatus.PublishedPartial or BusinessFinalizationStatus.NoContent;
        if (outcome.Operation != request.Operation || !completed || outcome.PersistenceStatus is not (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable))
            return BusinessFailure("business_publication_unavailable", indeterminate: outcome.Status == BusinessFinalizationStatus.CommitIndeterminate,
                interrupted: outcome.Status == BusinessFinalizationStatus.HardStopped || IsCancelled, disclose: false);
        _businessLogicPhase = outcome.Status switch
        {
            BusinessFinalizationStatus.Published => OctoberPhaseStatus.Completed,
            BusinessFinalizationStatus.PublishedPartial => OctoberPhaseStatus.Partial,
            _ => OctoberPhaseStatus.NoContent
        };
        _businessGuard.ConfirmCommitted(request.Operation);
        if (outcome.PersistenceStatus == KnowledgeWriteStatus.LogUnavailable) _businessPublicationLogUnavailable = true;
        if (_businessPublicationLogUnavailable) _gaps = _gaps.Add(new("operation_log_unavailable"));
        if (await DisclosureAsync(cancellationToken) != DisclosureStatus.Allowed)
            return BusinessFailure("business_authority_unavailable", disclose: false);
        ImmutableArray<DocumentHandle> documents = [];
        if (outcome.Status != BusinessFinalizationStatus.NoContent)
        {
            if (outcome.Publication is not { } publication || publication.Knowledge != binding || publication.PublicationId == Guid.Empty)
                return BusinessFailure("business_publication_binding_unavailable", disclose: false);
            documents = await ReadBusinessPublicationAsync(business, publication, cancellationToken);
            if (documents.IsDefaultOrEmpty) return BusinessFailure("business_publication_read_unavailable", disclose: false);
        }
        if (BusinessHandoffFailure(request) is { } beforeReturn) return beforeReturn;
        _businessDiagnostics = _businessDiagnostics with { Boundary = OctoberBusinessDiagnosticBoundary.Completed };
        return new("cl-and-bl", "cl-and-bl", outcome.Coverage.IsPartial ? OctoberCoverageStatus.Partial : OctoberCoverageStatus.Complete,
            _businessPublicationLogUnavailable || _businessResolutionFailed || _businessResolutionLimited ? OctoberExecutionStatus.Failed : OctoberExecutionStatus.Completed,
            outcome.Status == BusinessFinalizationStatus.NoContent ? OctoberPublicationStatus.NoContent : OctoberPublicationStatus.Confirmed,
            outcome.Publication, documents, _accepted, _coverage,
            new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(0, 0, 0, false), [])
        { Matching = _matchingPhase, BusinessLogic = _businessLogicPhase, BusinessDiagnostics = _businessDiagnostics };
    }

    private void ObserveBusinessOutcome(FinalizationRequest request, BusinessFinalizationResult result, bool finalizer)
    {
        bool exact = result.Operation == request.Operation;
        if (_existingClSnapshot is not null)
            _existingClSemanticUncertain = _businessDiagnostics?.OperationMismatch == true || !exact || result.Status == BusinessFinalizationStatus.CommitIndeterminate ||
                result.PersistenceStatus == KnowledgeWriteStatus.CommitIndeterminate;
        BusinessFinalizationStatus? status = exact && Enum.IsDefined(result.Status) ? result.Status : null;
        KnowledgeWriteStatus? persistence = exact && result.PersistenceStatus is { } observed && Enum.IsDefined(observed) ? observed : null;
        _businessDiagnostics = finalizer
            ? _businessDiagnostics! with { FinalizerStatus = status, FinalizerPersistenceStatus = persistence,
                OriginalSynthesisFailureReason = exact && result.OriginalSynthesisFailureReason is { } reason &&
                    reason != BusinessSynthesisFailureReason.None && Enum.IsDefined(reason) ? reason : null,
                OperationMismatch = _businessDiagnostics!.OperationMismatch || !exact }
            : _businessDiagnostics! with { LookupStatus = status, LookupPersistenceStatus = persistence,
                OperationMismatch = _businessDiagnostics!.OperationMismatch || !exact };
    }

    private OctoberScanReport? BusinessHandoffFailure(FinalizationRequest request)
    {
        var selection = request.Selection!;
        var handoff = _scope!.CheckHandoffEligibility(selection.RunId, request.Operation, selection.ProviderProfileId);
        return BusinessRunAvailable && handoff == ProviderAdmissionStatus.Granted ? null :
            BusinessFailure("business_run_ineligible", interrupted: IsCancelled || RemainingRun <= TimeSpan.Zero ||
                _execution!.Session.ExpiresAtUtc <= _time.GetUtcNow() ||
                handoff is ProviderAdmissionStatus.HardStopped or ProviderAdmissionStatus.DeadlineExceeded, disclose: false);
    }

    private async Task<ImmutableArray<DocumentHandle>> ReadBusinessPublicationAsync(OctoberBusinessRunComposition business,
        PublicationBinding publication, CancellationToken token)
    {
        var execution = _execution!;
        _businessDiagnostics = _businessDiagnostics! with { Boundary = OctoberBusinessDiagnosticBoundary.ReaderBinding };
        var binding = await StoreAsync(() => RetainBusinessOwnerCall(async () =>
        {
            var acquired = await business.BindReader!(execution.Session, publication, token);
            if (acquired?.Lifetime is { } lifetime)
                lock (_sync) _businessReaderLifetimes.Add(lifetime);
            return acquired;
        }), token);
        token.ThrowIfCancellationRequested();
        if (!BusinessRunAvailable || binding is null || binding.Subject is null || binding.Selection is null ||
            binding.Reader is null || binding.Lifetime is null || binding.Subject.SubjectId != execution.Session.SubjectId ||
            string.IsNullOrWhiteSpace(binding.Subject.AuthenticationBinding) ||
            binding.Selection.Subject != binding.Subject || binding.Selection.Publication != publication ||
            binding.Selection.Mode != KnowledgeMode.IT || binding.Selection.Purpose != KnowledgePurpose.Reader ||
            binding.Selection.RepositoryScope.IsDefault ||
            binding.Selection.RepositoryScope.Distinct().Count() != binding.Selection.RepositoryScope.Length ||
            !binding.Selection.RepositoryScope.Order().SequenceEqual(execution.Authority.RepositoryIds.Order()))
            return [];
        var reader = binding.Reader;
        _businessDiagnostics = _businessDiagnostics with { Boundary = OctoberBusinessDiagnosticBoundary.ExactReader };
        var opened = await StoreAsync(() => RetainBusinessOwnerCall(() => reader.OpenReadSelectionAsync(binding.Selection, token)), token);
        if (opened.Status != KnowledgeReadStatus.Success || opened.Selection is null) return [];
        return await ReadBusinessCatalogAsync(publication, opened.Selection,
            request => StoreAsync(() => RetainBusinessOwnerCall(() => reader.SearchAsync(request, token)), token),
            () => BusinessRunAvailable, token);
    }

    internal static async Task<ImmutableArray<DocumentHandle>> ReadBusinessCatalogAsync(PublicationBinding publication,
        ReadSelection selection, Func<KnowledgeSearchRequest, Task<SearchPage>> searchAsync,
        Func<bool> isAvailable, CancellationToken token)
    {
        var handles = ImmutableArray.CreateBuilder<DocumentHandle>();
        var continuations = new HashSet<string>(StringComparer.Ordinal);
        ContinuationToken? cursor = null;
        int pages = 0;
        int pageSize = KnowledgeContract.MaximumPageSize;
        do
        {
            token.ThrowIfCancellationRequested();
            if (!isAvailable() || handles.Count >= 4096 || pages++ >= 256) return [];
            var page = await searchAsync(new(selection, "", pageSize, cursor));
            token.ThrowIfCancellationRequested();
            if (page.Status == KnowledgeReadStatus.ReadBudgetExceeded && pageSize > 1)
            {
                // Cursors bind the requested page size. Restart this exact immutable selection;
                // retain the total-call limit across restarts and disclose only a complete catalog.
                pageSize /= 2;
                cursor = null;
                handles.Clear();
                continuations.Clear();
                continue;
            }
            if (page.Status != KnowledgeReadStatus.Success || page.Documents.IsDefault ||
                page.Documents.IsEmpty && page.Next is not null ||
                page.Documents.Any(document => document.Handle.BatchId != publication.Knowledge.BatchId ||
                    document.Handle.PublicationId != publication.PublicationId || document.Handle.LogicalDocumentId == Guid.Empty || document.Handle.DocumentVersionId == Guid.Empty))
                return [];
            foreach (var document in page.Documents)
            {
                if (handles.Contains(document.Handle) || handles.Count == 4096) return [];
                handles.Add(document.Handle);
            }
            cursor = page.Next;
            if (cursor is not null && !continuations.Add(cursor.Value)) return [];
            if (cursor is null) return handles.ToImmutable();
        } while (true);
    }

    private Task DisposeBusinessReadersAsync() => _businessReaderCleanup ??= DisposeBusinessReadersCoreAsync();

    private async Task DisposeBusinessReadersCoreAsync()
    {
        // Every acquisition registers its eventual lifetime before completing its
        // retained task; callers drain real reads before entering this cleanup.
        IAsyncDisposable[] lifetimes;
        lock (_sync) lifetimes = _businessReaderLifetimes.ToArray();
        foreach (var lifetime in lifetimes) await lifetime.DisposeAsync();
    }

    private static bool KnownCommitted(KnowledgeWriteStatus status) => status is
        KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable;

    private Task<T> RetainBusinessOwnerCall<T>(Func<Task<T>> start)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync) _businessOwnerCalls.Add(completion.Task);
        _ = CompleteOwnerCallAsync();
        return completion.Task;

        async Task CompleteOwnerCallAsync()
        {
            try
            {
                T result = await start();
                lock (_sync) completion.TrySetResult(result);
            }
            catch (OperationCanceledException error) { lock (_sync) completion.TrySetCanceled(error.CancellationToken); }
            catch (Exception error) { lock (_sync) completion.TrySetException(error); }
            finally
            {
                // Observe failures even when every bounded waiter has left.
                Observe(completion.Task);
                lock (_sync) _businessOwnerCalls.Remove(completion.Task);
            }
        }
    }

    private async Task AwaitBusinessOwnerCallsAsync()
    {
        while (true)
        {
            Task[] calls;
            lock (_sync) calls = _businessOwnerCalls.ToArray();
            if (calls.Length == 0) return;
            try { await Task.WhenAll(calls); }
            catch (Exception) { /* Each exact operation still owns its failure and reconciliation. */ }
            foreach (var call in calls)
            {
                Observe(call);
                lock (_sync) _businessOwnerCalls.Remove(call);
            }
        }
    }

    private sealed class ObservedBusinessPublicationStore(OctoberGenerateRunner owner,
        IOctoberFlowPublicationStore store, IOctoberFlowPublicationInitialRead initialRead)
        : IOctoberFlowPublicationStore, IOctoberFlowPublicationInitialRead
    {
        public Task<BusinessFinalizationInitialRead> ReadForOriginalDispatchAsync(CorpusHandle corpus,
            OperationKey operation, string canonicalPayloadDigest, CancellationToken cancellationToken = default) =>
            owner.RetainBusinessOwnerCall(() => initialRead.ReadForOriginalDispatchAsync(corpus, operation, canonicalPayloadDigest, cancellationToken));

        public Task<BusinessFinalizationResult> PublishAsync(FlowFinalizationPublicationRequest request,
            CancellationToken cancellationToken = default) => owner.RetainBusinessOwnerCall(async () =>
        {
            var result = await store.PublishAsync(request, cancellationToken);
            // A later successful lookup confirms durability but cannot erase the
            // original run's observed failure to write its mandatory log.
            if (result.PersistenceStatus == KnowledgeWriteStatus.LogUnavailable)
                owner._businessPublicationLogUnavailable = true;
            return result;
        });

        public Task<BusinessFinalizationResult> LookupAsync(CorpusHandle corpus, OperationKey operation,
            string canonicalPayloadDigest, CancellationToken cancellationToken = default) =>
            owner.RetainBusinessOwnerCall(() => store.LookupAsync(corpus, operation, canonicalPayloadDigest, cancellationToken));
    }

    private sealed class ObservedBusinessPlanningLoader(OctoberGenerateRunner owner, IOctoberFlowPlanningInputLoader loader)
        : IOctoberFlowPlanningInputLoader
    {
        public Task<FlowPlanningInputResult> LoadAsync(CorpusHandle corpus, ImmutableArray<FlowRootSelection> roots,
            CancellationToken cancellationToken = default) => owner.RetainBusinessOwnerCall(() => loader.LoadAsync(corpus, roots, cancellationToken));
    }

    private sealed class ObservedBusinessResolutionStore(OctoberGenerateRunner owner, IOctoberResolutionStore store)
        : IOctoberResolutionStore
    {
        public Task<ResolutionCorpusResult> LoadCorpusAsync(CorpusHandle corpus, CancellationToken cancellationToken = default) =>
            owner.RetainBusinessOwnerCall(() => store.LoadCorpusAsync(corpus, cancellationToken));
        public Task<ResolutionAcceptanceResult> AcceptOutcomeAsync(ResolutionAcceptanceRequest request, CancellationToken cancellationToken = default) =>
            owner.RetainBusinessOwnerCall(() => store.AcceptOutcomeAsync(request, cancellationToken));
        public Task<ResolutionAcceptanceResult> LookupOutcomeAsync(CorpusHandle corpus, OperationKey operation,
            string payloadDigest, CancellationToken cancellationToken = default) =>
            owner.RetainBusinessOwnerCall(() => store.LookupOutcomeAsync(corpus, operation, payloadDigest, cancellationToken));
    }

    private sealed class BusinessDisclosure(OctoberGenerateRunner owner) : IOctoberResolutionDisclosureAuthority
    {
        public Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors,
            CancellationToken cancellationToken = default) => corpus != owner._confirmedBusinessCorpus || !owner.BusinessRunAvailable
            ? Task.FromResult(DisclosureStatus.Denied) : owner.RetainBusinessOwnerCall(() => owner.CheckPermissionsAsync(contributors, cancellationToken));
    }
}
