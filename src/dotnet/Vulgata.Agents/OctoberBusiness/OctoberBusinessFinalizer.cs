using System.Collections.Immutable;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

/// <summary>Original-run orchestration only; Scan owns first dispatch and the store owns accepted publication.</summary>
public sealed class OctoberBusinessFinalizer : IOctoberBusinessFinalizer
{
    private readonly IOctoberOriginalBusinessFinalizationSlot? _slot;
    private readonly IOctoberFlowPublicationStore _store;
    private readonly IOctoberFlowPublicationInitialRead? _initialRead;
    private readonly IOctoberFlowPlanningInputLoader _loader;
    private readonly IOctoberResolutionDisclosureAuthority _disclosure;
    private readonly OctoberBusinessSynthesisExecutor _synthesis;
    private readonly string _profileId;
    private readonly string _configurationFingerprint;
    private readonly string _promptDigest;

    public OctoberBusinessFinalizer(IOctoberOriginalBusinessFinalizationSlot? originalSlot,
        IOctoberFlowPublicationStore store, IOctoberFlowPlanningInputLoader loader,
        IChatClient selectedClient, string providerProfileId,
        BusinessProviderConfigurationProjection providerConfiguration, string synthesisInstructions,
        IOctoberResolutionDisclosureAuthority disclosure, TimeProvider? timeProvider = null)
    {
        _slot = originalSlot;
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _initialRead = store as IOctoberFlowPublicationInitialRead;
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _disclosure = disclosure ?? throw new ArgumentNullException(nameof(disclosure));
        ArgumentException.ThrowIfNullOrWhiteSpace(providerProfileId);
        _profileId = providerProfileId;
        _configurationFingerprint = OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(providerConfiguration);
        _promptDigest = OctoberKnowledgePolicy.Hash(synthesisInstructions);
        _synthesis = new(selectedClient, synthesisInstructions, timeProvider);
    }

    public async Task<BusinessFinalizationResult> FinalizeAsync(FinalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_slot is null || _initialRead is null || request.ExecutionScope is not IOctoberBusinessHandoffLifetime lifetime)
            return Empty(request, BusinessFinalizationStatus.AuthorityUnavailable);
        try
        {
            if (request.Selection is not { } selection || selection.ProviderProfileId != _profileId ||
                selection.ProviderConfigurationFingerprint != _configurationFingerprint || selection.PromptDigest != _promptDigest ||
                request.CanonicalPayloadDigest != OctoberBusinessFinalizationPolicy.ComputeRequestDigest(request))
                return Empty(request, BusinessFinalizationStatus.Denied);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { return Empty(request, BusinessFinalizationStatus.Denied); }
        if (Eligibility(request, lifetime) is { } ineligible) return Empty(request, ineligible);
        try
        {
            // No caller token reaches the original callback; the privately issued slot supplies the host token.
            await _slot.RunOnceAsync(request, hostToken => RunOriginalAsync(request, lifetime, hostToken), cancellationToken)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { throw; }
        catch (Exception)
        {
            // Joining a failed or interrupted original task never authorizes another invocation.
        }
        // The cached result is never disclosure authority, even for the first waiter.
        try
        {
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.HandoffCancellation);
            bounded.CancelAfter(TimeSpan.FromSeconds(10));
            var result = await AwaitAsync(_store.LookupAsync(request.Corpus, request.Operation,
                request.CanonicalPayloadDigest, bounded.Token), bounded.Token);
            if (Eligibility(request, lifetime) is { } stopped) return Empty(request, stopped);
            bounded.Token.ThrowIfCancellationRequested();
            return Coherent(request, result) ? Sanitize(request, result) : Empty(request, BusinessFinalizationStatus.CommitIndeterminate);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        { return Empty(request, Eligibility(request, lifetime) ?? BusinessFinalizationStatus.CommitIndeterminate); }
    }

    private async Task<BusinessFinalizationResult> RunOriginalAsync(FinalizationRequest request,
        IOctoberBusinessHandoffLifetime lifetime, CancellationToken hostToken)
    {
        using var original = CancellationTokenSource.CreateLinkedTokenSource(hostToken, lifetime.HandoffCancellation);
        var token = original.Token;
        try
        {
            if (Eligibility(request, lifetime) is { } stopped) return Empty(request, stopped);
            BusinessFinalizationInitialRead observed;
            using (var bounded = Bound(token))
                observed = await AwaitAsync(_initialRead!.ReadForOriginalDispatchAsync(request.Corpus, request.Operation,
                    request.CanonicalPayloadDigest, bounded.Token), bounded.Token);
            if (observed is null || !Enum.IsDefined(observed.Kind)) return Empty(request, BusinessFinalizationStatus.CommitIndeterminate);
            if (observed.Kind == BusinessFinalizationInitialReadKind.NoDispatchResult)
                return observed.Result is { } result && Coherent(request, result)
                    ? Sanitize(request, result) : Empty(request, BusinessFinalizationStatus.CommitIndeterminate);
            if (observed.Result is not null || observed.Kind != BusinessFinalizationInitialReadKind.ObservedAbsent)
                return Empty(request, BusinessFinalizationStatus.CommitIndeterminate);
            if (Eligibility(request, lifetime) is { } unavailable) return Empty(request, unavailable);
            var selection = request.Selection!;
            FlowPlanningInputResult loaded;
            using (var bounded = Bound(token))
                loaded = await AwaitAsync(_loader.LoadAsync(request.Corpus, selection.Roots, bounded.Token), bounded.Token);
            if (loaded.Status != KnowledgeReadStatus.Success || loaded.Input is not { } input || input.Corpus.Handle != request.Corpus ||
                input.Roots.IsDefault || !input.Roots.ToHashSet().SetEquals(selection.Roots))
                return Empty(request, BusinessFinalizationStatus.AuthorityUnavailable);
            var plan = OctoberFlowPlanner.Plan(input, selection.Limits);
            if (plan.Status != FlowPlanningStatus.Succeeded) return Empty(request, BusinessFinalizationStatus.Failed);
            var completed = ImmutableArray.CreateBuilder<FlowSynthesisSubmission>();
            var dispositions = ImmutableArray.CreateBuilder<FlowFinalizationDisposition>();
            bool stopFurtherAttempts = false;
            foreach (var root in selection.Roots)
            {
                token.ThrowIfCancellationRequested();
                if (Eligibility(request, lifetime) is { } ineligible) return Empty(request, ineligible);
                var group = plan.Groups.SingleOrDefault(x => x.Flow.RootUnitId == root.UnitId && x.RootVersion == root.Document &&
                    x.Flow.RootKind == root.Kind && x.Flow.RootSelector == root.Selector);
                if (group is null || stopFurtherAttempts)
                {
                    dispositions.Add(new(root, group?.GroupVersionId, FlowFinalizationDispositionKind.NotEvaluated,
                        group is null ? "Selected root was not supported by the bounded planner." : "Original synthesis allowance did not permit further work."));
                    continue;
                }
                var synthesized = await _synthesis.ExecuteAsync(input, group.GroupVersionId, request.Operation, selection.RunId,
                    selection.ProviderProfileId, selection.MaximumTokens, request.ExecutionScope, _disclosure, token, selection.Limits);
                if (synthesized.AdmissionFailure is ProviderAdmissionStatus.Denied or ProviderAdmissionStatus.AuthorityUnavailable or ProviderAdmissionStatus.HardStopped)
                    return Empty(request, Map(synthesized.AdmissionFailure.Value));
                if (synthesized.Content is not null && synthesized.Dependencies is not null && synthesized.Proposal is not null && synthesized.AdmissionFailure is null)
                    completed.Add(new(root, group.GroupVersionId, synthesized.Proposal));
                else
                {
                    dispositions.Add(new(root, group.GroupVersionId, FlowFinalizationDispositionKind.Failed, "Synthesis did not produce a validated completed proposal."));
                    stopFurtherAttempts = synthesized.AdmissionFailure is ProviderAdmissionStatus.BudgetExhausted or ProviderAdmissionStatus.DeadlineExceeded;
                }
            }
            if (Eligibility(request, lifetime) is { } fenced) return Empty(request, fenced);
            using (var bounded = Bound(token))
            {
                var authority = await AwaitAsync(_disclosure.CheckAsync(request.Corpus, input.Contributors, bounded.Token), bounded.Token);
                if (authority != DisclosureStatus.Allowed) return Empty(request, authority == DisclosureStatus.Denied
                    ? BusinessFinalizationStatus.Denied : BusinessFinalizationStatus.AuthorityUnavailable);
            }
            if (Eligibility(request, lifetime) is { } denied) return Empty(request, denied);
            token.ThrowIfCancellationRequested();
            var publication = new FlowFinalizationPublicationRequest(request.Operation, request.Corpus, request.CanonicalPayloadDigest,
                selection, completed.ToImmutable(), dispositions.ToImmutable(), "");
            publication = publication with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(publication) };
            using var publishBound = Bound(token);
            var saved = await AwaitAsync(_store.PublishAsync(publication, publishBound.Token), publishBound.Token);
            return Coherent(request, saved) ? Sanitize(request, saved) : Empty(request, BusinessFinalizationStatus.CommitIndeterminate);
        }
        catch (OperationCanceledException)
        { return Empty(request, Eligibility(request, lifetime) ?? (hostToken.IsCancellationRequested ? BusinessFinalizationStatus.HardStopped : BusinessFinalizationStatus.CommitIndeterminate)); }
        catch (Exception) { return Empty(request, BusinessFinalizationStatus.CommitIndeterminate); }
    }

    private static bool Coherent(FinalizationRequest request, BusinessFinalizationResult result)
    {
        if (result is null || result.Operation != request.Operation || !Enum.IsDefined(result.Status) || result.Coverage is null ||
            result.Coverage.PermittedGaps.IsDefault || result.PersistenceStatus is { } persistence && !Enum.IsDefined(persistence))
            return false;
        // A missing legacy diagnostic stays missing; it is not synthesized as a commit receipt.
        if (result.PersistenceStatus == KnowledgeWriteStatus.Committed && result.IsReplay ||
            result.PersistenceStatus == KnowledgeWriteStatus.AlreadyCommitted && !result.IsReplay)
            return false;
        bool published = result.Status is BusinessFinalizationStatus.Published or BusinessFinalizationStatus.PublishedPartial;
        bool completed = published || result.Status == BusinessFinalizationStatus.NoContent;
        if (completed && result.PersistenceStatus is not (null or KnowledgeWriteStatus.Committed or
                KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable))
            return false;
        if (result.Status == BusinessFinalizationStatus.Published && result.Coverage.IsPartial ||
            result.Status == BusinessFinalizationStatus.PublishedPartial && !result.Coverage.IsPartial)
            return false;
        return published
            ? result.Publication is { PublicationId: var id } && id != Guid.Empty && result.Publication.Knowledge == request.Corpus.Binding
            : result.Publication is null;
    }

    private static BusinessFinalizationResult Sanitize(FinalizationRequest request, BusinessFinalizationResult result) =>
        result.Status is BusinessFinalizationStatus.Published or BusinessFinalizationStatus.PublishedPartial or BusinessFinalizationStatus.NoContent
            ? result : Empty(request, result.Status) with { PersistenceStatus = result.PersistenceStatus, IsReplay = result.IsReplay };

    private static BusinessFinalizationResult Empty(FinalizationRequest request, BusinessFinalizationStatus status) =>
        new(status, request.Operation, null, new(true, []));

    private static BusinessFinalizationStatus? Eligibility(FinalizationRequest request, IOctoberBusinessHandoffLifetime lifetime)
    {
        try
        {
            var selection = request.Selection!;
            var status = lifetime.CheckHandoffEligibility(selection.RunId, request.Operation, selection.ProviderProfileId);
            return status == ProviderAdmissionStatus.Granted ? null : Map(status);
        }
        catch (Exception) { return BusinessFinalizationStatus.AuthorityUnavailable; }
    }

    private static BusinessFinalizationStatus Map(ProviderAdmissionStatus status) => status switch
    {
        ProviderAdmissionStatus.Denied => BusinessFinalizationStatus.Denied,
        ProviderAdmissionStatus.AuthorityUnavailable => BusinessFinalizationStatus.AuthorityUnavailable,
        ProviderAdmissionStatus.HardStopped or ProviderAdmissionStatus.DeadlineExceeded => BusinessFinalizationStatus.HardStopped,
        _ => BusinessFinalizationStatus.Failed
    };
    private static CancellationTokenSource Bound(CancellationToken token)
    {
        var bound = CancellationTokenSource.CreateLinkedTokenSource(token);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        return bound;
    }
    private static async Task<T> AwaitAsync<T>(Task<T> task, CancellationToken token)
    {
        _ = ObserveAsync(task);
        return await task.WaitAsync(token);
    }
    private static async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch (Exception) { }
    }
}
