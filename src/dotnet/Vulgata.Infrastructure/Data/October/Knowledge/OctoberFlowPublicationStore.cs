using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Owner boundary for atomic rooted BL publication and exact finalization reconciliation.</summary>
public sealed class OctoberFlowPublicationStore : IOctoberFlowPublicationStore, IOctoberFlowPublicationInitialRead
{
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly IOctoberKnowledgeProducerAuthorization _authorization;
    private readonly IOctoberCommitSubmissionGuard _guard;
    private readonly IOctoberKnowledgeOperationLog _log;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public OctoberFlowPublicationStore(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberKnowledgeProducerAuthorization producerAuthorization, IOctoberCommitSubmissionGuard commitGuard,
        IOctoberKnowledgeOperationLog operationLog)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _authorization = producerAuthorization ?? throw new ArgumentNullException(nameof(producerAuthorization));
        _guard = commitGuard ?? throw new ArgumentNullException(nameof(commitGuard));
        _log = operationLog ?? throw new ArgumentNullException(nameof(operationLog));
    }

    public async Task<BusinessFinalizationResult> PublishAsync(FlowFinalizationPublicationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var previous = await ReadAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest, request,
            KnowledgeWriteStatus.AlreadyCommitted, true, cancellationToken);
        if (previous.Exists != false) return previous.Result;
        try
        {
            if (OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) != request.CanonicalOutcomeDigest)
                return Failure(request.Operation, KnowledgeWriteStatus.InvalidProposal);
        }
        catch (ArgumentException) { return Failure(request.Operation, KnowledgeWriteStatus.InvalidProposal); }
        var committer = new OctoberKnowledgePublicationCommitter(_contexts, _authorization, _guard, _log);
        var mutation = await committer.ExecuteAsync(request.Corpus.Binding, request.Operation, OperationKind.FinalizeCorpus,
            request.CanonicalPayloadDigest,
            () => OctoberBusinessFinalizationPolicy.ComputeRequestDigest(new(request.Operation, request.Corpus, "", null!) { Selection = request.Selection }),
            _ => Task.FromResult<KnowledgeWriteStatus?>(null),
            (context, token) => PrepareAsync(context, request, token), cancellationToken);
        if (mutation.Status is not (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable or KnowledgeWriteStatus.CommitIndeterminate))
            return Failure(request.Operation, mutation.Status);
        if (mutation.Status == KnowledgeWriteStatus.CommitIndeterminate && cancellationToken.IsCancellationRequested)
            return Indeterminate(request.Operation);
        using var reconciliation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reconciled = await ReadAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest, request,
            mutation.Status == KnowledgeWriteStatus.CommitIndeterminate ? KnowledgeWriteStatus.AlreadyCommitted : mutation.Status,
            false, reconciliation.Token);
        return reconciled.Result;
    }

    public async Task<BusinessFinalizationResult> LookupAsync(CorpusHandle corpus, OperationKey operation,
        string canonicalPayloadDigest, CancellationToken cancellationToken = default) =>
        (await ReadAsync(corpus, operation, canonicalPayloadDigest, null, KnowledgeWriteStatus.AlreadyCommitted, true, cancellationToken)).Result;

    public async Task<BusinessFinalizationInitialRead> ReadForOriginalDispatchAsync(CorpusHandle corpus, OperationKey operation,
        string canonicalPayloadDigest, CancellationToken cancellationToken = default)
    {
        var observed = await ReadAsync(corpus, operation, canonicalPayloadDigest, null, KnowledgeWriteStatus.AlreadyCommitted, true, cancellationToken);
        if (observed.Exists == false)
            return new(BusinessFinalizationInitialReadKind.ObservedAbsent, null);
        if (observed.Result.Status is BusinessFinalizationStatus.CommitIndeterminate or BusinessFinalizationStatus.AuthorityUnavailable)
            return new(BusinessFinalizationInitialReadKind.Unavailable, null);
        return new(BusinessFinalizationInitialReadKind.NoDispatchResult, observed.Result);
    }

    private async Task<PreparedPublicationMutation> PrepareAsync(OctoberDbContext context, FlowFinalizationPublicationRequest request, CancellationToken ct)
    {
        var loaded = await new OctoberFlowPlanningInputLoader(_contexts, _authorization).LoadAsync(request.Corpus, request.Selection.Roots, ct);
        if (loaded.Status != KnowledgeReadStatus.Success || loaded.Input is not { } input)
            return Rejected(request, KnowledgeWriteStatus.Denied);
        var plan = OctoberFlowPlanner.Plan(input, request.Selection.Limits);
        if (plan.Status != FlowPlanningStatus.Succeeded ||
            plan.RootDispositions.Any(x => x.Kind is not (FlowRootDispositionKind.Planned or FlowRootDispositionKind.UnsupportedKind)))
            return Rejected(request, KnowledgeWriteStatus.InvalidProposal);
        var validated = new List<(FlowGroupVersion Group, BusinessLogicContent Content)>();
        foreach (var completed in request.Completed)
        {
            var group = plan.Groups.SingleOrDefault(x => x.RootVersion == completed.Root.Document && x.Flow.RootUnitId == completed.Root.UnitId &&
                x.Flow.RootKind == completed.Root.Kind && x.Flow.RootSelector == completed.Root.Selector && x.GroupVersionId == completed.GroupVersionId);
            if (group is null) return Rejected(request, KnowledgeWriteStatus.InvalidProposal);
            var candidate = OctoberBusinessSynthesisPolicy.Validate(input, group.GroupVersionId, completed.Proposal, request.Selection.Limits);
            if (candidate.Status != BusinessSynthesisValidationStatus.Succeeded || candidate.Content is null || candidate.Dependencies is null ||
                !Same(candidate.Dependencies, group.Dependencies)) return Rejected(request, KnowledgeWriteStatus.InvalidProposal);
            validated.Add((group, candidate.Content));
        }
        foreach (var disposition in request.Dispositions)
        {
            var rootDisposition = plan.RootDispositions.SingleOrDefault(x => x.Root == disposition.Root);
            if (rootDisposition is null) return Rejected(request, KnowledgeWriteStatus.InvalidProposal);
            if (rootDisposition.Kind == FlowRootDispositionKind.UnsupportedKind)
            {
                if (disposition.Kind != FlowFinalizationDispositionKind.NotEvaluated || disposition.GroupVersionId is not null)
                    return Rejected(request, KnowledgeWriteStatus.InvalidProposal);
                continue;
            }
            var group = plan.Groups.Single(x => x.RootVersion == disposition.Root.Document && x.Flow.RootUnitId == disposition.Root.UnitId && x.Flow.RootKind == disposition.Root.Kind && x.Flow.RootSelector == disposition.Root.Selector);
            if (disposition.GroupVersionId is { } version && version != group.GroupVersionId) return Rejected(request, KnowledgeWriteStatus.InvalidProposal);
        }
        Guid batch = request.Operation.BatchId;
        Guid corpusId = request.Corpus.CorpusId;
        PublicationBinding? publication = validated.Count == 0 ? null : new(request.Corpus.Binding, Guid.NewGuid());
        bool partial = request.Dispositions.Length != 0 || input.Coverage.Any(x => x.Disposition is not (CoverageDisposition.Accepted or CoverageDisposition.CompletedZeroUnit));
        var coverage = new CoverageProjection(partial, partial ? ["部分处理未完成；已保留可用结果及其限制。"] : []);
        var status = publication is not null ? partial ? BusinessFinalizationStatus.PublishedPartial : BusinessFinalizationStatus.Published :
            request.Selection.Roots.IsEmpty ? BusinessFinalizationStatus.NoContent : BusinessFinalizationStatus.Failed;
        var result = new BusinessFinalizationResult(status, request.Operation, publication, coverage);
        string requestJson = OctoberKnowledgePolicy.SerializeCanonical(request);
        string resultJson = OctoberKnowledgePolicy.SerializeCanonical(result);
        string contributorsJson = OctoberKnowledgePolicy.SerializeCanonical(input.Contributors);
        // A committed result must fit the exact lookup envelope, including both copies of the
        // full corpus ledger. Use the conservative four-byte string bound for every provider;
        // PostgreSQL's UTF-8/jsonb projection is no larger than this accepted envelope allowance.
        try
        {
            var replayBudget = new OctoberKnowledgeStoredReadBudget();
            replayBudget.Add([contributorsJson.Length, requestJson.Length, resultJson.Length, contributorsJson.Length]);
        }
        catch (OctoberKnowledgeReadBudgetException) { return Rejected(request, KnowledgeWriteStatus.InvalidProposal); }
        context.BusinessFinalizations.Add(new()
        {
            BatchId = batch, OperationId = request.Operation.OperationId, CorpusId = corpusId, PublicationId = publication?.PublicationId,
            RequestDigest = request.CanonicalPayloadDigest, OutcomeDigest = request.CanonicalOutcomeDigest,
            RequestJson = requestJson, ResultJson = resultJson, ContributorsJson = contributorsJson
        });
        if (publication is not null)
        {
            context.KnowledgePublications.Add(new()
            {
                BatchId = batch, PublicationId = publication.PublicationId, CorpusId = corpusId, CorpusDigest = request.Corpus.CorpusDigest,
                OperationKind = OperationKind.FinalizeCorpus, OperationId = request.Operation.OperationId,
                ContributorsJson = OctoberKnowledgePolicy.SerializeCanonical(input.Contributors), CreatedAtUtc = DateTimeOffset.UtcNow
            });
            var members = input.Corpus.Documents.Select(x => x.Version).ToList();
            foreach (var (group, content) in validated)
                members.Add(await AddFlowAsync(context, request, group, content, ct));
            var ordered = OctoberKnowledgePublicationPolicy.OrderedDocuments(members.ToImmutableArray());
            for (int ordinal = 0; ordinal < ordered.Length; ordinal++)
                context.PublicationMembers.Add(new() { BatchId = batch, PublicationId = publication.PublicationId, CorpusId = corpusId,
                    LogicalDocumentId = ordered[ordinal].LogicalDocumentId, VersionId = ordered[ordinal].DocumentVersionId, Ordinal = ordinal });
            foreach (var row in await context.CorpusCoverage.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct))
                context.PublicationCoverage.Add(new() { BatchId = batch, PublicationId = publication.PublicationId, CorpusId = corpusId,
                    AdmissionId = row.AdmissionId, RepositoryId = row.RepositoryId, CoverageJson = row.CoverageJson, ContributorsJson = row.ContributorsJson });
            foreach (var edge in input.Resolutions.Where(x => x.Outcome.Relationship is not null).Select(x => x.Outcome.Relationship!))
                context.PublicationRelationships.Add(new() { BatchId = batch, PublicationId = publication.PublicationId, CorpusId = corpusId,
                    RelationshipVersionId = edge.RelationshipVersionId, FromLogicalDocumentId = edge.From.LogicalDocumentId, FromVersionId = edge.From.DocumentVersionId,
                    ToLogicalDocumentId = edge.To.LogicalDocumentId, ToVersionId = edge.To.DocumentVersionId });
            foreach (var unknown in input.Resolutions.Where(x => x.UncertaintyVersionId is not null))
                context.PublicationResolutionUncertainties.Add(new() { BatchId = batch, PublicationId = publication.PublicationId, CorpusId = corpusId, UncertaintyId = unknown.UncertaintyVersionId!.Value });
            foreach (var unknown in input.FileUncertainties)
                context.PublicationUncertainties.Add(new() { BatchId = batch, PublicationId = publication.PublicationId, CorpusId = corpusId, ManifestId = unknown.ManifestId, Key = unknown.Key });
        }
        return new(new(KnowledgeWriteStatus.Committed, request.Operation, Corpus: request.Corpus, Publication: publication), input.Contributors);
    }

    private static async Task<DocumentVersionReference> AddFlowAsync(OctoberDbContext context, FlowFinalizationPublicationRequest request,
        FlowGroupVersion group, BusinessLogicContent content, CancellationToken ct)
    {
        Guid batch = request.Operation.BatchId;
        Guid corpus = request.Corpus.CorpusId;
        Guid flowId = group.Flow.LogicalFlowId;
        Guid logicalId = Id(new { domain = "october/business-document/1", batch, flowId });
        if (!await context.LogicalFlows.AnyAsync(x => x.BatchId == batch && x.LogicalFlowId == flowId, ct))
        {
            context.LogicalFlows.Add(new() { BatchId = batch, LogicalFlowId = flowId, RootUnitId = group.Flow.RootUnitId,
                RootKind = group.Flow.RootKind, RootSelector = group.Flow.RootSelector, HomeRepositoryId = group.Flow.HomeRepositoryId, OperationId = request.Operation.OperationId });
            context.LogicalDocuments.Add(new() { BatchId = batch, LogicalDocumentId = logicalId, Kind = DocumentKind.BusinessLogic, LogicalFlowId = flowId });
        }
        else if (!await context.LogicalDocuments.AnyAsync(x => x.BatchId == batch && x.LogicalDocumentId == logicalId && x.LogicalFlowId == flowId && x.Kind == DocumentKind.BusinessLogic, ct))
            throw new JsonException("Missing immutable BL logical identity.");
        var retained = await context.FlowGroupVersions.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batch && x.GroupVersionId == group.GroupVersionId, ct);
        if (retained is not null && !Same(Required<FlowGroupVersion>(retained.GroupJson), group)) throw new JsonException("Conflicting immutable flow group.");
        if (retained is null)
        {
            context.FlowGroupVersions.Add(new() { BatchId = batch, GroupVersionId = group.GroupVersionId, CorpusId = corpus, LogicalFlowId = flowId,
                RootLogicalDocumentId = group.RootVersion.LogicalDocumentId, RootVersionId = group.RootVersion.DocumentVersionId,
                OperationId = request.Operation.OperationId, GroupJson = OctoberKnowledgePolicy.SerializeCanonical(group), ContributorsJson = OctoberKnowledgePolicy.SerializeCanonical(group.Dependencies.Contributors) });
            foreach (var document in group.Dependencies.Documents)
                context.FlowDocumentDependencies.Add(new() { BatchId = batch, GroupVersionId = group.GroupVersionId, CorpusId = corpus,
                    LogicalDocumentId = document.LogicalDocumentId, VersionId = document.DocumentVersionId, IsMember = group.Members.Contains(document) });
            foreach (var edge in group.Dependencies.Relationships)
                context.FlowRelationshipDependencies.Add(new() { BatchId = batch, GroupVersionId = group.GroupVersionId, CorpusId = corpus,
                    RelationshipVersionId = edge.RelationshipVersionId, IsMember = group.Relationships.Any(x => x.RelationshipVersionId == edge.RelationshipVersionId) });
            foreach (var unknown in group.Dependencies.Uncertainties)
                context.FlowResolutionUncertaintyDependencies.Add(new() { BatchId = batch, GroupVersionId = group.GroupVersionId, CorpusId = corpus,
                    UncertaintyId = unknown.UncertaintyVersionId, IsSliceQualifier = group.Uncertainties.Any(x => x.UncertaintyVersionId == unknown.UncertaintyVersionId) });
            foreach (var unknown in group.Dependencies.FileUncertainties)
                context.FlowFileUncertaintyDependencies.Add(new() { BatchId = batch, GroupVersionId = group.GroupVersionId, CorpusId = corpus, ManifestId = unknown.ManifestId, Key = unknown.Key });
        }
        Guid versionId = Id(new { domain = "october/business-version/1", batch, logicalId, group.GroupVersionId,
            contentDigest = OctoberKnowledgePolicy.Hash(content.ContentJson), request.Operation });
        context.DocumentVersions.Add(new() { BatchId = batch, LogicalDocumentId = logicalId, VersionId = versionId, Kind = DocumentKind.BusinessLogic,
            FlowGroupVersionId = group.GroupVersionId, FinalizationOperationKind = OperationKind.FinalizeCorpus, FinalizationOperationId = request.Operation.OperationId,
            SchemaVersion = KnowledgeContract.SchemaVersion, Title = content.Title, ContentJson = content.ContentJson,
            ContentDigest = OctoberKnowledgePolicy.Hash(content.ContentJson), AnchorsJson = "[]", CreatedAtUtc = DateTimeOffset.UtcNow });
        foreach (var section in content.Sections)
        {
            context.DocumentSections.Add(new() { BatchId = batch, VersionId = versionId, SectionId = section.SectionId, Ordinal = section.Ordinal, Title = section.Title });
            foreach (var claim in section.Claims)
                context.ClaimGroups.Add(new() { BatchId = batch, VersionId = versionId, SectionId = section.SectionId, ClaimGroupId = claim.ClaimGroupId,
                    Text = claim.Text, Qualification = claim.Qualification, EvidenceJson = OctoberKnowledgePolicy.SerializeCanonical(claim.Evidence),
                    LimitationsJson = OctoberKnowledgePolicy.SerializeCanonical(claim.Limitations) });
        }
        return new(batch, logicalId, versionId);
    }

    private async Task<(bool? Exists, BusinessFinalizationResult Result)> ReadAsync(CorpusHandle corpus, OperationKey operation, string digest,
        FlowFinalizationPublicationRequest? proposed, KnowledgeWriteStatus persistence, bool log, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var ct = deadline.Token;
        OctoberDbContext? context = null;
        Task<(bool? Exists, BusinessFinalizationResult Result)>? reading = null;
        try
        {
            var creating = _contexts.CreateDbContextAsync(ct);
            try { context = await creating.WaitAsync(ct); }
            catch { _ = DisposeLateAsync(creating); throw; }
            ct.ThrowIfCancellationRequested();
            reading = ReadCoreAsync(context, corpus, operation, digest, proposed, persistence, log, ct);
            var result = await reading.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception) { return (null, Indeterminate(operation)); }
        finally
        {
            if (context is not null)
            {
                if (reading is { IsCompleted: false }) _ = DisposeAfterAsync(context, reading);
                else await context.DisposeAsync();
            }
        }
    }

    private async Task<(bool? Exists, BusinessFinalizationResult Result)> ReadCoreAsync(OctoberDbContext context, CorpusHandle corpus,
        OperationKey operation, string digest, FlowFinalizationPublicationRequest? proposed, KnowledgeWriteStatus persistence, bool log, CancellationToken ct)
    {
        Guid batch = operation.BatchId;
        Guid id = operation.OperationId;
        if (operation.Kind != OperationKind.FinalizeCorpus || batch != corpus.Binding.BatchId || id == Guid.Empty ||
            digest is null || digest.Length != 64 || !digest.All(char.IsAsciiHexDigit))
            return (null, Failure(operation, KnowledgeWriteStatus.InvalidProposal));
        var budget = new OctoberKnowledgeStoredReadBudget();
        if (context.Database.IsNpgsql())
        {
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContributorsJson"::text)::bigint AS "Value" FROM october."OctoberCorpusSelections"
                WHERE "BatchId"={batch} AND "CorpusId"={corpus.CorpusId} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("RequestJson"::text)::bigint+octet_length("ResultJson"::text)+octet_length("ContributorsJson"::text) AS "Value"
                FROM october."OctoberBusinessFinalizations" WHERE "BatchId"={batch} AND "OperationKind"=3 AND "OperationId"={id} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
        }
        else
        {
            budget.Add(await context.CorpusSelections.Where(x => x.BatchId == batch && x.CorpusId == corpus.CorpusId).Select(x => (long)x.ContributorsJson.Length).Take(2).ToListAsync(ct), 1);
            budget.Add(await context.BusinessFinalizations.Where(x => x.BatchId == batch && x.OperationKind == OperationKind.FinalizeCorpus && x.OperationId == id)
                .Select(x => (long)x.RequestJson.Length + x.ResultJson.Length + x.ContributorsJson.Length).Take(2).ToListAsync(ct), 1);
        }
        var retained = await OctoberResolutionStore.FindCorpusAsync(context, corpus, ct);
        if (retained is null) return (null, Failure(operation, KnowledgeWriteStatus.Denied));
        var ledger = Required<ImmutableArray<Contributor>>(retained.ContributorsJson);
        var authority = await AuthorizeAsync(corpus.Binding, ledger, ct);
        if (authority != DisclosureStatus.Allowed) return (null, Failure(operation, OctoberKnowledgePublicationCommitter.AuthorityStatus(authority)));
        var stored = await context.BusinessFinalizations.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batch && x.OperationKind == OperationKind.FinalizeCorpus && x.OperationId == id, ct);
        if (stored is null)
        {
            if (retained.CorpusDigest != corpus.CorpusDigest) return (null, Failure(operation, KnowledgeWriteStatus.Conflict));
            // Absence is an observation only when neither side of the atomic terminal write is
            // visible. A retained operation without its expected result is inconsistent storage,
            // not an unused original-dispatch slot.
            if (await context.KnowledgeOperations.AsNoTracking().AnyAsync(x => x.BatchId == batch &&
                x.Kind == OperationKind.FinalizeCorpus && x.OperationId == id, ct))
                return (null, Indeterminate(operation));
            authority = await AuthorizeAsync(corpus.Binding, ledger, ct);
            if (authority != DisclosureStatus.Allowed) return (null, Failure(operation, OctoberKnowledgePublicationCommitter.AuthorityStatus(authority)));
            ct.ThrowIfCancellationRequested();
            return (false, Indeterminate(operation));
        }
        var contributors = Required<ImmutableArray<Contributor>>(stored.ContributorsJson);
        authority = await AuthorizeAsync(corpus.Binding, contributors, ct);
        if (authority != DisclosureStatus.Allowed) return (true, Failure(operation, OctoberKnowledgePublicationCommitter.AuthorityStatus(authority)));
        if (stored.CorpusId != corpus.CorpusId || retained.CorpusDigest != corpus.CorpusDigest || stored.RequestDigest != digest ||
            proposed is not null && (stored.OutcomeDigest != proposed.CanonicalOutcomeDigest || OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(proposed) != stored.OutcomeDigest))
            return (true, Failure(operation, KnowledgeWriteStatus.Conflict));
        var original = Required<FlowFinalizationPublicationRequest>(stored.RequestJson);
        if (original.CanonicalPayloadDigest != stored.RequestDigest || original.CanonicalOutcomeDigest != stored.OutcomeDigest ||
            OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(original) != stored.OutcomeDigest)
            return (true, Indeterminate(operation));
        var result = Required<BusinessFinalizationResult>(stored.ResultJson);
        if (result.Operation != operation || result.Publication?.PublicationId != stored.PublicationId || result.PersistenceStatus is not null || result.IsReplay)
            return (true, Indeterminate(operation));
        bool published = result.Status is BusinessFinalizationStatus.Published or BusinessFinalizationStatus.PublishedPartial;
        bool validStatus = result.Status is BusinessFinalizationStatus.Published or BusinessFinalizationStatus.PublishedPartial or
            BusinessFinalizationStatus.NoContent or BusinessFinalizationStatus.Failed;
        if (!validStatus || result.Coverage is null || result.Coverage.PermittedGaps.IsDefault ||
            result.Coverage.PermittedGaps.Any(string.IsNullOrWhiteSpace) ||
            published != (result.Publication is not null) ||
            published && (result.Publication!.Knowledge != corpus.Binding || result.Publication.PublicationId == Guid.Empty || original.Completed.IsEmpty) ||
            !published && (!original.Completed.IsEmpty || stored.PublicationId is not null) ||
            result.Status == BusinessFinalizationStatus.PublishedPartial && !result.Coverage.IsPartial ||
            result.Status == BusinessFinalizationStatus.Published && result.Coverage.IsPartial ||
            result.Status == BusinessFinalizationStatus.NoContent && !original.Selection.Roots.IsEmpty ||
            result.Status == BusinessFinalizationStatus.Failed && (original.Selection.Roots.IsEmpty || original.Dispositions.IsEmpty))
            return (true, Indeterminate(operation));
        if (published && !await context.KnowledgePublications.AsNoTracking().AnyAsync(x => x.BatchId == batch &&
            x.PublicationId == result.Publication!.PublicationId && x.CorpusId == corpus.CorpusId && x.CorpusDigest == corpus.CorpusDigest &&
            x.OperationKind == OperationKind.FinalizeCorpus && x.OperationId == id, ct))
            return (true, Indeterminate(operation));
        if (log)
        {
            try { if (!await _log.AppendAsync(new(corpus.Binding, operation, persistence), ct).WaitAsync(ct)) persistence = KnowledgeWriteStatus.LogUnavailable; }
            catch (Exception) { persistence = KnowledgeWriteStatus.LogUnavailable; }
        }
        authority = await AuthorizeAsync(corpus.Binding, contributors, ct);
        if (authority != DisclosureStatus.Allowed) return (true, Failure(operation, OctoberKnowledgePublicationCommitter.AuthorityStatus(authority)));
        return (true, result with { PersistenceStatus = persistence, IsReplay = persistence == KnowledgeWriteStatus.AlreadyCommitted || log });
    }

    private async Task<DisclosureStatus> AuthorizeAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var result = await _authorization.CheckAsync(binding, contributors, ct).WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return DisclosureStatus.AuthorityUnavailable; }
    }

    private static PreparedPublicationMutation Rejected(FlowFinalizationPublicationRequest request, KnowledgeWriteStatus status) => new(new(status, request.Operation), []);
    private static BusinessFinalizationResult Failure(OperationKey operation, KnowledgeWriteStatus status) => new(
        status switch { KnowledgeWriteStatus.Denied => BusinessFinalizationStatus.Denied,
            KnowledgeWriteStatus.AuthorityUnavailable => BusinessFinalizationStatus.AuthorityUnavailable,
            KnowledgeWriteStatus.StaleOrFenced or KnowledgeWriteStatus.CancelledBeforeCommit => BusinessFinalizationStatus.HardStopped,
            _ => BusinessFinalizationStatus.Failed }, operation, null, new(true, [])) { PersistenceStatus = status };
    private static Guid Id<T>(T value) => new(Convert.FromHexString(OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(value)))[..16]);
    private static bool Same<T>(T left, T right) => OctoberKnowledgePolicy.SerializeCanonical(left) == OctoberKnowledgePolicy.SerializeCanonical(right);
    private static T Required<T>(string json) => JsonSerializer.Deserialize<T>(json, _json) ?? throw new JsonException("Missing finalization record.");
    private static async Task DisposeLateAsync(Task<OctoberDbContext> task) { try { await using var context = await task; } catch (Exception) { } }
    private static async Task DisposeAfterAsync(OctoberDbContext context, Task reading)
    {
        try { await reading; } catch (Exception) { }
        finally { try { await context.DisposeAsync(); } catch (Exception) { } }
    }

    private static BusinessFinalizationResult Indeterminate(OperationKey operation) =>
        new(BusinessFinalizationStatus.CommitIndeterminate, operation, null, new(true, []))
        {
            PersistenceStatus = KnowledgeWriteStatus.CommitIndeterminate
        };
}
