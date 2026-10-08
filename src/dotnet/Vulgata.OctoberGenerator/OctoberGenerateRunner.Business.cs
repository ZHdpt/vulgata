using System.Collections.Immutable;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>Trusted controlled composition; the command entry point does not supply it.</summary>
internal sealed record OctoberBusinessRunComposition(
    Func<IOctoberKnowledgeProducerAuthorization, IOctoberCommitSubmissionGuard, IOctoberResolutionStore> CreateResolutionStore,
    Func<IOctoberKnowledgeProducerAuthorization, IOctoberFlowPlanningInputLoader> CreatePlanningInputLoader,
    Func<IOctoberKnowledgeProducerAuthorization, IOctoberCommitSubmissionGuard, IOctoberFlowPublicationStore> CreatePublicationStore,
    Func<PublicationBinding, IOctoberKnowledgeReader> CreateReader,
    Func<FlowPlanningInput, ImmutableArray<FlowRootSelection>> SelectRoots,
    BusinessProviderConfigurationProjection ProviderConfiguration,
    string ResolutionInstructions,
    string SynthesisInstructions,
    FlowPlanningLimits Limits,
    long MaximumAttemptTokens);

internal sealed partial class OctoberGenerateRunner
{
    private OperationKey? _businessFinalizationOperation;
    private IChatClient? _businessClient;
    private BusinessCommitGuard? _businessGuard;
    private Task? _sourceCleanup;
    private bool _businessPublicationLogUnavailable;
    private bool _businessResolutionFailed;
    private bool _businessResolutionLimited;
    private readonly HashSet<Task> _businessOwnerCalls = [];

    private bool BusinessRunAvailable => _execution is not null && RunAvailable &&
        _execution.Session.ExpiresAtUtc > _time.GetUtcNow() && _runToken is { IsCancellationRequested: false };

    private bool BusinessConfigurationMatches(OctoberGenerateExecution execution)
    {
        if (execution.Policy.OutputStage != "cl-and-bl" || _dependencies.Business is not { } business ||
            business.CreateResolutionStore is null || business.CreatePlanningInputLoader is null ||
            business.CreatePublicationStore is null || business.CreateReader is null || business.SelectRoots is null ||
            business.ProviderConfiguration is not { } configuration || business.Limits is not { } limits ||
            string.IsNullOrWhiteSpace(business.ResolutionInstructions) || string.IsNullOrWhiteSpace(business.SynthesisInstructions) ||
            business.ResolutionInstructions.Length > KnowledgeContract.MaximumContentBytes ||
            business.SynthesisInstructions.Length > KnowledgeContract.MaximumContentBytes ||
            business.MaximumAttemptTokens <= 0 || business.MaximumAttemptTokens > execution.Policy.MaximumTotalTokens ||
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

    private Task CloseSourceResourcesAsync() => _sourceCleanup ??= _source is null
        ? Task.CompletedTask : _source.DisposeAsync().AsTask();

    private async Task<OctoberScanReport> RunBusinessCompletionAsync(CancellationToken cancellationToken)
    {
        var business = _dependencies.Business!;
        var execution = _execution!;
        var binding = _binding!;
        if (!BusinessRunAvailable || await CheckPermissionsAsync(_observedContributors, cancellationToken) != DisclosureStatus.Allowed)
            return Failure("business_authority_unavailable", interrupted: IsCancelled, disclose: false);
        _businessGuard = new(this);
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
            frozen.Corpus is not { } corpus || corpus.Binding != binding || corpus.CorpusId == Guid.Empty ||
            corpus.CorpusDigest != _expectedFreeze.CanonicalPayloadDigest)
            return Failure("business_freeze_unavailable", indeterminate: frozen.Status == KnowledgeWriteStatus.CommitIndeterminate,
                interrupted: IsCancelled, disclose: false);
        _businessGuard.ConfirmCommitted(_expectedFreeze.Operation);
        _confirmedBusinessCorpus = corpus;
        if (frozen.Status == KnowledgeWriteStatus.LogUnavailable)
            return Failure("operation_log_unavailable");
        if (!BusinessRunAvailable) return Failure("business_run_ineligible", interrupted: true);

        _businessClient ??= await AcquireClientAsync(execution.Provider, cancellationToken);
        if (_businessClient is not IOctoberBusinessAttemptClient bound || bound.ProviderProfileId != execution.Provider.Profile.Name)
            return Failure("business_client_unavailable");
        var resolutionStore = new ObservedBusinessResolutionStore(this, business.CreateResolutionStore(this, _businessGuard));
        var disclosure = new BusinessDisclosure(this);
        var loaded = await StoreAsync(() => resolutionStore.LoadCorpusAsync(corpus, cancellationToken), cancellationToken);
        if (loaded.Status != KnowledgeReadStatus.Success || loaded.Corpus is not { } acceptedCorpus || acceptedCorpus.Handle != corpus)
            return Failure("business_corpus_unavailable", disclose: false);
        var resolutionPlan = OctoberResolutionPolicy.Plan(acceptedCorpus);
        var resolver = new OctoberResolutionExecutor(_businessClient, business.ResolutionInstructions, _time);
        foreach (var inquiry in resolutionPlan.Inquiries)
        {
            if (!BusinessRunAvailable) return Failure("business_run_ineligible", interrupted: true);
            var resolved = await resolver.ExecuteAsync(acceptedCorpus, inquiry, _businessFinalizationOperation!,
                _beginRequest!.RunId, execution.Provider.Profile.Name, business.MaximumAttemptTokens, _scope!, disclosure, cancellationToken);
            if (resolved.AdmissionFailure is ProviderAdmissionStatus.Denied or ProviderAdmissionStatus.AuthorityUnavailable or ProviderAdmissionStatus.HardStopped)
                return Failure("business_resolution_ineligible", interrupted: IsCancelled, disclose: false);
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
                return Failure("business_resolution_unavailable", indeterminate: saved.Status == KnowledgeWriteStatus.CommitIndeterminate, disclose: false);
            _businessGuard.ConfirmCommitted(acceptance.Operation);
            if (saved.Outcome.Kind == ResolutionOutcomeKind.Failed)
            {
                _businessResolutionFailed = true;
                if (!_gaps.Any(gap => gap.Code == "business_resolution_failed"))
                    _gaps = _gaps.Add(new("business_resolution_failed"));
            }
            if (notEvaluatedForLimit && saved.Outcome.Kind == ResolutionOutcomeKind.NotEvaluated)
            {
                _businessResolutionLimited = true;
                if (!_gaps.Any(gap => gap.Code == "business_resolution_limit"))
                    _gaps = _gaps.Add(new("business_resolution_limit"));
            }
            if (saved.Status == KnowledgeWriteStatus.LogUnavailable) return Failure("operation_log_unavailable");
        }
        var loader = new ObservedBusinessPlanningLoader(this, business.CreatePlanningInputLoader(this));
        var planning = await StoreAsync(() => loader.LoadAsync(corpus, [], cancellationToken), cancellationToken);
        if (planning.Status != KnowledgeReadStatus.Success || planning.Input is not { } input || input.Corpus.Handle != corpus || input.Contributors.IsDefault)
            return Failure("business_planning_inputs_unavailable", disclose: false);
        _observedContributors = input.Contributors;
        if (!BusinessRunAvailable || await CheckPermissionsAsync(input.Contributors, cancellationToken) != DisclosureStatus.Allowed)
            return Failure("business_authority_unavailable", interrupted: IsCancelled, disclose: false);
        var roots = business.SelectRoots(input);
        if (roots.IsDefault) return Failure("business_roots_unavailable");
        var selection = new BusinessFinalizationSelection(roots, business.Limits, _beginRequest!.RunId,
            execution.Provider.Profile.Name, OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(business.ProviderConfiguration),
            OctoberKnowledgePolicy.Hash(business.SynthesisInstructions), business.MaximumAttemptTokens);
        var request = new FinalizationRequest(_businessFinalizationOperation!, corpus, "", _scope!) { Selection = selection };
        request = request with { CanonicalPayloadDigest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(request) };
        _originalBusinessRequest = request;
        _businessProviderConfiguration = business.ProviderConfiguration;
        var slot = TryIssueOriginalBusinessFinalizationSlot();
        if (slot is null) return Failure("business_original_slot_unavailable", interrupted: IsCancelled, disclose: false);
        var ownerPublications = business.CreatePublicationStore(this, _businessGuard);
        if (ownerPublications is not IOctoberFlowPublicationInitialRead initialRead) return Failure("business_initial_read_unavailable");
        var publications = new ObservedBusinessPublicationStore(this, ownerPublications, initialRead);
        _businessGuard.Register(new(request.Operation, request.Operation.OperationId, request.CanonicalPayloadDigest));
        var finalizer = new OctoberBusinessFinalizer(slot, publications, loader, _businessClient,
            execution.Provider.Profile.Name, business.ProviderConfiguration, business.SynthesisInstructions, disclosure, _time);
        try { await finalizer.FinalizeAsync(request, cancellationToken); }
        finally { await AwaitBusinessOwnerCallsAsync(); }
        // Bounded orchestration may finish before an actual owner call returns.
        // Its late result is settlement evidence, never fresh disclosure authority.
        if (BusinessHandoffFailure(request) is { } beforeLookup) return beforeLookup;
        BusinessFinalizationResult outcome;
        try
        {
            outcome = await StoreAsync(() => publications.LookupAsync(corpus, request.Operation,
                request.CanonicalPayloadDigest, cancellationToken), cancellationToken);
        }
        finally { await AwaitBusinessOwnerCallsAsync(); }
        if (BusinessHandoffFailure(request) is { } afterLookup) return afterLookup;
        bool completed = outcome.Status is BusinessFinalizationStatus.Published or BusinessFinalizationStatus.PublishedPartial or BusinessFinalizationStatus.NoContent;
        if (outcome.Operation != request.Operation || !completed || outcome.PersistenceStatus is not (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable))
            return Failure("business_publication_unavailable", indeterminate: outcome.Status == BusinessFinalizationStatus.CommitIndeterminate,
                interrupted: outcome.Status == BusinessFinalizationStatus.HardStopped || IsCancelled, disclose: false);
        _businessGuard.ConfirmCommitted(request.Operation);
        if (outcome.PersistenceStatus == KnowledgeWriteStatus.LogUnavailable) _businessPublicationLogUnavailable = true;
        if (_businessPublicationLogUnavailable) _gaps = _gaps.Add(new("operation_log_unavailable"));
        if (await DisclosureAsync(cancellationToken) != DisclosureStatus.Allowed)
            return Failure("business_authority_unavailable", disclose: false);
        ImmutableArray<DocumentHandle> documents = [];
        if (outcome.Status != BusinessFinalizationStatus.NoContent)
        {
            if (outcome.Publication is not { } publication || publication.Knowledge != binding || publication.PublicationId == Guid.Empty)
                return Failure("business_publication_binding_unavailable", disclose: false);
            documents = await ReadBusinessPublicationAsync(business, publication, cancellationToken);
            if (documents.IsDefaultOrEmpty) return Failure("business_publication_read_unavailable", disclose: false);
        }
        if (BusinessHandoffFailure(request) is { } beforeReturn) return beforeReturn;
        return new("cl-and-bl", "cl-and-bl", outcome.Coverage.IsPartial ? OctoberCoverageStatus.Partial : OctoberCoverageStatus.Complete,
            _businessPublicationLogUnavailable || _businessResolutionFailed || _businessResolutionLimited ? OctoberExecutionStatus.Failed : OctoberExecutionStatus.Completed,
            outcome.Status == BusinessFinalizationStatus.NoContent ? OctoberPublicationStatus.NoContent : OctoberPublicationStatus.Confirmed,
            outcome.Publication, documents, _accepted, _coverage,
            new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(0, 0, 0, false), []);
    }

    private OctoberScanReport? BusinessHandoffFailure(FinalizationRequest request)
    {
        var selection = request.Selection!;
        var handoff = _scope!.CheckHandoffEligibility(selection.RunId, request.Operation, selection.ProviderProfileId);
        return BusinessRunAvailable && handoff == ProviderAdmissionStatus.Granted ? null :
            Failure("business_run_ineligible", interrupted: IsCancelled || RemainingRun <= TimeSpan.Zero ||
                _execution!.Session.ExpiresAtUtc <= _time.GetUtcNow() ||
                handoff is ProviderAdmissionStatus.HardStopped or ProviderAdmissionStatus.DeadlineExceeded, disclose: false);
    }

    private async Task<ImmutableArray<DocumentHandle>> ReadBusinessPublicationAsync(OctoberBusinessRunComposition business,
        PublicationBinding publication, CancellationToken token)
    {
        var reader = business.CreateReader(publication);
        var execution = _execution!;
        var opened = await StoreAsync(() => RetainBusinessOwnerCall(() => reader.OpenReadSelectionAsync(new(publication,
            new(execution.Session.SubjectId, execution.Session.ManagementAuthorityId), KnowledgeMode.IT, KnowledgePurpose.Reader,
            execution.Authority.RepositoryIds.ToImmutableArray()), token)), token);
        if (opened.Status != KnowledgeReadStatus.Success || opened.Selection is null) return [];
        var handles = ImmutableArray.CreateBuilder<DocumentHandle>();
        var continuations = new HashSet<string>(StringComparer.Ordinal);
        ContinuationToken? cursor = null;
        int pages = 0;
        do
        {
            if (!BusinessRunAvailable || handles.Count >= 4096 || pages++ >= 256) return [];
            var page = await StoreAsync(() => RetainBusinessOwnerCall(() => reader.SearchAsync(new(opened.Selection, "", KnowledgeContract.MaximumPageSize, cursor), token)), token);
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
        } while (cursor is not null);
        return handles.ToImmutable();
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
