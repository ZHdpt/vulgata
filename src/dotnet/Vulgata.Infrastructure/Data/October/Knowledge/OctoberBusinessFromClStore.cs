using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Original-owner admission and cleanup receipts in the existing KnowledgeOperations journal.</summary>
public sealed class OctoberBusinessFromClStore : IOctoberBusinessFromClStore
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly IOctoberBusinessFromClAuthority _authority;
    private readonly IOctoberCommitSubmissionGuard _beginGuard, _completionGuard;
    private readonly IOctoberBusinessFromClSettlementAuthority _settlement;
    private readonly IOctoberKnowledgeOperationLog _log;
    private readonly Lock _sync = new();
    private readonly Dictionary<OperationKey, Admission> _owners = [];

    public OctoberBusinessFromClStore(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberBusinessFromClAuthority authority, IOctoberCommitSubmissionGuard beginGuard,
        IOctoberCommitSubmissionGuard completionGuard, IOctoberBusinessFromClSettlementAuthority settlementAuthority,
        IOctoberKnowledgeOperationLog log)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _beginGuard = beginGuard ?? throw new ArgumentNullException(nameof(beginGuard));
        _completionGuard = completionGuard ?? throw new ArgumentNullException(nameof(completionGuard));
        _settlement = settlementAuthority ?? throw new ArgumentNullException(nameof(settlementAuthority));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<BusinessFromClResult> BeginAsync(BusinessFromClRequest request, CancellationToken cancellationToken = default)
    {
        var operation = OctoberBusinessFromClPolicy.BeginOperation(request);
        if (!Valid(request)) return Task.FromResult(new BusinessFromClResult(KnowledgeWriteStatus.InvalidProposal, operation));
        Admission owner;
        TaskCompletionSource<BusinessFromClResult> completion;
        lock (_sync)
        {
            if (_owners.TryGetValue(operation, out var existing))
            {
                if (existing.Request.CanonicalPayloadDigest != request.CanonicalPayloadDigest)
                    return Task.FromResult(new BusinessFromClResult(KnowledgeWriteStatus.Conflict, operation));
                // A repeated request observes fresh permissions through Lookup; it is never another dispatch grant.
                return LookupAsync(request, cancellationToken);
            }
            owner = new(this, request);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            owner.BeginTask = completion.Task;
            _owners.Add(operation, owner);
        }
        _ = BeginOwnedAsync(owner, completion, cancellationToken);
        return completion.Task;
    }

    private async Task BeginOwnedAsync(Admission owner, TaskCompletionSource<BusinessFromClResult> completion, CancellationToken token)
    {
        BusinessFromClRequest request = owner.Request;
        OperationKey operation = OctoberBusinessFromClPolicy.BeginOperation(request);
        try
        {
            var authorization = new RequestAuthorization(this, request);
            var guard = new BeginGuard(_beginGuard, owner);
            var committer = new OctoberKnowledgePublicationCommitter(_contexts, authorization, guard, _log);
            BusinessFromClSnapshot? prepared = null;
            var result = await committer.ExecuteAsync(request.Publication.Knowledge, operation, OperationKind.BeginBusinessFromCl,
                request.CanonicalPayloadDigest, () => OctoberBusinessFromClPolicy.ComputeBeginDigest(request),
                _ => Task.FromResult<KnowledgeWriteStatus?>(null), async (context, ct) =>
                {
                    var unmatched = await context.KnowledgeOperations.AsNoTracking().Where(begin =>
                        begin.BatchId == operation.BatchId && begin.Kind == OperationKind.BeginBusinessFromCl &&
                        !context.KnowledgeOperations.Any(end => end.BatchId == begin.BatchId &&
                            end.Kind == OperationKind.CompleteBusinessFromCl && end.OperationId == begin.OperationId)).Take(257).ToListAsync(ct);
                    if (unmatched.Count > 256) return new(new(KnowledgeWriteStatus.StorageUnavailable, operation), []);
                    foreach (var begin in unmatched)
                    {
                        var abandoned = await FindAsync(context, new(begin.BatchId, OperationKind.AbandonBusinessFromCl, begin.OperationId), ct);
                        if (!OctoberBusinessFromClRecoveryStore.ValidTerminal(begin, abandoned))
                            return new(new(KnowledgeWriteStatus.Conflict, operation), []);
                    }
                    try
                    {
                        prepared = await LoadAsync(context, request, ct);
                        owner.Corpus = prepared.Corpus;
                        return new(new(KnowledgeWriteStatus.Committed, operation, Corpus: prepared.Corpus, Publication: request.Publication), prepared.Contributors);
                    }
                    catch (AuthorityFailure error) { return new(new(error.Status, operation), []); }
                    catch (JsonException) { return new(new(KnowledgeWriteStatus.InvalidProposal, operation), []); }
                }, token, request.RunId);
            await ConfirmOriginalAsync(owner);
            if (result.Status is not (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable))
            { completion.TrySetResult(new(result.Status, operation, Admission: Capability(owner))); return; }
            // A prior receipt without this instance's original physical submission cannot issue ownership.
            BusinessFromClResult disclosed = await ReadBeginAsync(request, owner, token);
            if (disclosed.Status == KnowledgeWriteStatus.AlreadyCommitted)
                disclosed = disclosed with { Status = result.Status };
            completion.TrySetResult(disclosed);
        }
        catch (AuthorityFailure failure) { completion.TrySetResult(new(failure.Status, operation, Admission: Capability(owner))); }
        catch (Exception) { completion.TrySetResult(new(owner.Submitted ? KnowledgeWriteStatus.CommitIndeterminate : KnowledgeWriteStatus.StorageUnavailable, operation, Admission: Capability(owner))); }
    }

    public async Task<BusinessFromClResult> LookupAsync(BusinessFromClRequest request, CancellationToken cancellationToken = default)
    {
        var operation = OctoberBusinessFromClPolicy.BeginOperation(request);
        if (!Valid(request)) return new(KnowledgeWriteStatus.InvalidProposal, operation);
        Admission? owner;
        lock (_sync) _owners.TryGetValue(operation, out owner);
        if (owner is not null && !ReferenceEquals(owner.Request, request)) owner = null;
        if (owner is not null) await ConfirmOriginalAsync(owner);
        return await ReadBeginAsync(request, owner, cancellationToken);
    }

    private async Task<BusinessFromClResult> ReadBeginAsync(BusinessFromClRequest request, Admission? owner, CancellationToken token)
    {
        var operation = OctoberBusinessFromClPolicy.BeginOperation(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await using var context = await OpenAsync(deadline.Token);
            var snapshot = await LoadAsync(context, request, deadline.Token);
            var row = await FindAsync(context, operation, deadline.Token);
            if (row is null) return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
            if (row.PayloadDigest != request.CanonicalPayloadDigest) return new(KnowledgeWriteStatus.Conflict, operation);
            var stored = OctoberKnowledgeOperationStore.Result(row);
            if (stored.Status != KnowledgeWriteStatus.Committed || stored.Operation != operation || stored.Corpus != snapshot.Corpus ||
                stored.Publication != request.Publication || stored.AcceptedFile is not null ||
                !SameSet(OctoberKnowledgeOperationStore.Contributors(row), snapshot.Contributors)) return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
            await AuthorizeAsync(request, snapshot.Contributors, deadline.Token);
            var terminal = await context.KnowledgeOperations.AsNoTracking().AnyAsync(x => x.BatchId == operation.BatchId &&
                x.OperationId == operation.OperationId && (x.Kind == OperationKind.AbandonBusinessFromCl || x.Kind == OperationKind.CompleteBusinessFromCl), deadline.Token);
            return new(KnowledgeWriteStatus.AlreadyCommitted, operation, snapshot, terminal ? null : Capability(owner));
        }
        catch (AuthorityFailure failure) { return new(failure.Status, operation, Admission: Capability(owner)); }
        catch (Exception) { return new(KnowledgeWriteStatus.CommitIndeterminate, operation, Admission: Capability(owner)); }
    }

    private async Task ConfirmOriginalAsync(Admission owner)
    {
        if (!owner.Submitted || owner.Committed) return;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await using var context = await OpenAsync(deadline.Token);
            var operation = OctoberBusinessFromClPolicy.BeginOperation(owner.Request);
            var row = await FindAsync(context, operation, deadline.Token);
            if (row is null || row.PayloadDigest != owner.Request.CanonicalPayloadDigest || owner.Corpus is null) return;
            var stored = OctoberKnowledgeOperationStore.Result(row);
            if (stored.Status == KnowledgeWriteStatus.Committed && stored.Operation == operation && stored.Corpus == owner.Corpus &&
                stored.Publication == owner.Request.Publication && stored.AcceptedFile is null) owner.Committed = true;
        }
        catch (Exception) { /* No receipt is never proof of rollback. */ }
    }

    public async Task<KnowledgeWriteResult> CompleteAsync(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission,
        CancellationToken cancellationToken = default)
    {
        var operation = OctoberBusinessFromClPolicy.CompleteOperation(request.Begin);
        if (!Owns(request, admission, out Admission? owner)) return new(KnowledgeWriteStatus.Denied, operation);
        var authorization = new CleanupAuthorization(this, request, owner!);
        var guard = new CompletionGuard(this, request, owner!, _completionGuard);
        var committer = new OctoberKnowledgePublicationCommitter(_contexts, authorization, guard, _log);
        return await committer.ExecuteAsync(request.Begin.Publication.Knowledge, operation, OperationKind.CompleteBusinessFromCl,
            request.CanonicalPayloadDigest, () => OctoberBusinessFromClPolicy.ComputeCompletionDigest(request),
            ct => VerifySettlementAsync(request, owner!, ct), async (context, ct) =>
            {
                var begin = await FindAsync(context, OctoberBusinessFromClPolicy.BeginOperation(request.Begin), ct);
                if (begin is null || begin.PayloadDigest != request.Begin.CanonicalPayloadDigest)
                    return new(new(KnowledgeWriteStatus.Conflict, operation), []);
                // Only this receipt is appended. No corpus, source or publication reads occur during cleanup.
                return new(new(KnowledgeWriteStatus.Committed, operation), []);
            }, cancellationToken, request.Begin.RunId);
    }

    public async Task<KnowledgeWriteResult> LookupCompletionAsync(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission,
        CancellationToken cancellationToken = default)
    {
        var operation = OctoberBusinessFromClPolicy.CompleteOperation(request.Begin);
        if (!Owns(request, admission, out _)) return new(KnowledgeWriteStatus.Denied, operation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await using var context = await OpenAsync(deadline.Token);
            var row = await FindAsync(context, operation, deadline.Token);
            if (row is null) return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
            if (row.PayloadDigest != request.CanonicalPayloadDigest) return new(KnowledgeWriteStatus.Conflict, operation);
            var stored = OctoberKnowledgeOperationStore.Result(row);
            if (stored.Status != KnowledgeWriteStatus.Committed || stored.Operation != operation || stored.AcceptedFile is not null ||
                stored.Corpus is not null || stored.Publication is not null || !OctoberKnowledgeOperationStore.Contributors(row).IsEmpty)
                return new(KnowledgeWriteStatus.CommitIndeterminate, operation);
            return new(KnowledgeWriteStatus.AlreadyCommitted, operation);
        }
        catch (Exception) { return new(KnowledgeWriteStatus.CommitIndeterminate, operation); }
    }

    private async Task<BusinessFromClSnapshot> LoadAsync(OctoberDbContext context, BusinessFromClRequest request, CancellationToken ct)
    {
        var binding = request.Publication.Knowledge;
        Guid batch = binding.BatchId, publicationId = request.Publication.PublicationId;
        if (!await context.Batches.AsNoTracking().AnyAsync(x => x.BatchId == batch && x.TargetId == binding.TargetId &&
            x.CanonicalDatabaseIdentity == binding.CanonicalDatabaseIdentity && x.SelectionDigest == binding.SelectionDigest, ct))
            throw new AuthorityFailure(KnowledgeWriteStatus.Denied);
        var selected = await context.KnowledgePublications.AsNoTracking().Where(x => x.BatchId == batch && x.PublicationId == publicationId)
            .Select(x => new { x.CorpusId, x.CorpusDigest, x.OperationKind, x.OperationId }).SingleOrDefaultAsync(ct);
        if (selected is null || selected.OperationKind != OperationKind.PublishSelection) throw new JsonException();
        Guid corpusId = selected.CorpusId;
        var corpus = new CorpusHandle(binding, corpusId, selected.CorpusDigest);
        var budget = new OctoberKnowledgeStoredReadBudget();
        if (context.Database.IsNpgsql())
        {
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContributorsJson"::text)::bigint AS "Value"
                FROM october."OctoberKnowledgePublications" WHERE "BatchId"={batch} AND "PublicationId"={publicationId} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContributorsJson"::text)::bigint AS "Value"
                FROM october."OctoberCorpusSelections" WHERE "BatchId"={batch} AND "CorpusId"={corpusId} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("CoverageJson"::text)::bigint + octet_length("ContributorsJson"::text) AS "Value"
                FROM october."OctoberPublicationCoverage" WHERE "BatchId"={batch} AND "PublicationId"={publicationId} LIMIT 10001
                """).ToListAsync(ct), bytes: true);
        }
        else
        {
            budget.Add(await context.KnowledgePublications.Where(x => x.BatchId == batch && x.PublicationId == publicationId)
                .Select(x => (long)x.ContributorsJson.Length).Take(2).ToListAsync(ct), 1);
            budget.Add(await context.CorpusSelections.Where(x => x.BatchId == batch && x.CorpusId == corpusId)
                .Select(x => (long)x.ContributorsJson.Length).Take(2).ToListAsync(ct), 1);
            budget.Add(await context.PublicationCoverage.Where(x => x.BatchId == batch && x.PublicationId == publicationId)
                .Select(x => (long)x.CoverageJson.Length + x.ContributorsJson.Length).Take(10001).ToListAsync(ct));
        }
        var retained = await OctoberResolutionStore.FindCorpusAsync(context, corpus, ct);
        var publication = await context.KnowledgePublications.AsNoTracking().SingleAsync(x => x.BatchId == batch && x.PublicationId == publicationId, ct);
        if (retained is null || retained.CorpusDigest != corpus.CorpusDigest) throw new JsonException();
        var contributors = Required<ImmutableArray<Contributor>>(retained.ContributorsJson);
        if (contributors.IsDefault || contributors.Length > OctoberKnowledgeStoredReadBudget.MaximumContributors || contributors.Any(x => x is null) ||
            !SameSet(contributors, Required<ImmutableArray<Contributor>>(publication.ContributorsJson))) throw new JsonException();
        await AuthorizeAsync(request, contributors, ct);
        await OctoberFlowPlanningInputLoader.PreflightAsync(context, corpus, budget, ct);
        var resolutionCorpus = await OctoberResolutionStore.LoadExactCorpusAsync(context, corpus, ct, budget);
        var members = await context.CorpusDocuments.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).Take(10001).ToListAsync(ct);
        var published = await context.PublicationMembers.AsNoTracking().Where(x => x.BatchId == batch && x.PublicationId == publicationId).Take(10001).ToListAsync(ct);
        var exactDocuments = members.Select(x => new DocumentVersionReference(batch, x.LogicalDocumentId, x.VersionId)).ToImmutableArray();
        if (members.Count > 10000 || published.Count != members.Count || published.Any(x => x.CorpusId != corpusId) ||
            !SameSet(exactDocuments, published.Select(x => new DocumentVersionReference(batch, x.LogicalDocumentId, x.VersionId))) ||
            !SameSet(exactDocuments, resolutionCorpus.Documents.Select(x => x.Version))) throw new JsonException();
        var manifestIds = await context.CorpusManifests.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => x.ManifestId).ToArrayAsync(ct);
        var manifests = await context.AcceptedFileManifests.AsNoTracking().Where(x => x.BatchId == batch && manifestIds.Contains(x.ManifestId)).ToListAsync(ct);
        var coverageRows = await context.CorpusCoverage.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var publishedCoverage = await context.PublicationCoverage.AsNoTracking().Where(x => x.BatchId == batch && x.PublicationId == publicationId).ToListAsync(ct);
        var coverage = coverageRows.Select(x => Required<FileCoverage>(x.CoverageJson)).ToImmutableArray();
        var contributorRows = await context.KnowledgeContributors.AsNoTracking().Where(x => x.BatchId == batch && manifestIds.Contains(x.ManifestId)).ToListAsync(ct);
        if (manifestIds.Length != manifests.Count || coverage.Length != publishedCoverage.Count ||
            coverage.Select(x => x.AdmissionId).Distinct().Count() != coverage.Length ||
            !SameSet(contributors, coverageRows.SelectMany(x => Required<ImmutableArray<Contributor>>(x.ContributorsJson)))) throw new JsonException();
        foreach (var row in coverageRows)
        {
            var item = Required<FileCoverage>(row.CoverageJson);
            var rowContributors = Required<ImmutableArray<Contributor>>(row.ContributorsJson);
            var copy = publishedCoverage.SingleOrDefault(x => x.AdmissionId == row.AdmissionId);
            if (copy is null || copy.CorpusId != corpusId || copy.RepositoryId != row.RepositoryId ||
                !Same(Required<FileCoverage>(copy.CoverageJson), item) ||
                !SameSet(Required<ImmutableArray<Contributor>>(copy.ContributorsJson), rowContributors) ||
                !OctoberKnowledgePublicationPolicy.ValidCoverage(item) || item.AdmissionId != row.AdmissionId || item.Origin.RepositoryId != row.RepositoryId)
                throw new JsonException();
            var manifest = manifests.SingleOrDefault(x => x.AdmissionId == item.AdmissionId);
            if (manifest is not null)
            {
                if (!OctoberKnowledgePublicationPolicy.SameCoverage(item, Coverage(manifest)) ||
                    !SameSet(rowContributors, contributorRows.Where(x => x.ManifestId == manifest.ManifestId).Select(x => Required<Contributor>(x.ContributorJson))))
                    throw new JsonException();
            }
            else
            {
                var repository = await context.RepositorySelections.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batch && x.RepositoryId == row.RepositoryId, ct);
                if (item.Disposition is CoverageDisposition.Accepted or CoverageDisposition.CompletedZeroUnit || repository is null ||
                    repository.RootBindingIdentity != item.Origin.RootBindingIdentity ||
                    !SameSet(rowContributors, [new Contributor(ContributorKind.Source, repository.RepositoryId, repository.SystemId,
                        item.Origin.RootBindingIdentity, null, item.Origin.SourceDigest)])) throw new JsonException();
            }
        }
        var accepted = ImmutableArray.CreateBuilder<AcceptedFile>();
        foreach (var manifest in manifests.OrderBy(x => x.ManifestId))
        {
            var documents = members.Where(x => x.ManifestId == manifest.ManifestId)
                .Select(x => new DocumentVersionReference(batch, x.LogicalDocumentId, x.VersionId)).ToImmutableArray();
            if (documents.Length != manifest.UnitCount || !coverage.Any(x => x.AdmissionId == manifest.AdmissionId)) throw new JsonException();
            foreach (var document in documents)
                if (!await context.DocumentVersions.AsNoTracking().AnyAsync(x => x.BatchId == batch && x.VersionId == document.DocumentVersionId &&
                    x.LogicalDocumentId == document.LogicalDocumentId && x.ManifestId == manifest.ManifestId && x.Kind == DocumentKind.CodeLogic, ct)) throw new JsonException();
            accepted.Add(new(manifest.ManifestId, manifest.AdmissionId, manifest.ProposalDigest, documents, Coverage(manifest)));
        }
        if (members.Any(x => !manifestIds.Contains(x.ManifestId))) throw new JsonException();
        var acceptedFiles = accepted.ToImmutable();
        if (OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(new(new(batch, retained.OperationKind, retained.OperationId), binding,
            retained.CorpusDigest, acceptedFiles, coverage)) != retained.CorpusDigest) throw new JsonException();
        var publicationReceipt = await FindAsync(context, new(batch, selected.OperationKind, selected.OperationId), ct);
        if (publicationReceipt is null) throw new JsonException();
        var publishedResult = OctoberKnowledgeOperationStore.Result(publicationReceipt);
        if (publishedResult.Status != KnowledgeWriteStatus.Committed || publishedResult.Publication != request.Publication ||
            publishedResult.Operation != new OperationKey(batch, selected.OperationKind, selected.OperationId) || publishedResult.AcceptedFile is not null ||
            !SameSet(OctoberKnowledgeOperationStore.Contributors(publicationReceipt), contributors)) throw new JsonException();
        var outcomes = await LoadPartialOutcomesAsync(context, resolutionCorpus, contributors, ct);
        await AuthorizeAsync(request, contributors, ct);
        return new(request.Publication, corpus, acceptedFiles, OctoberKnowledgePublicationPolicy.OrderedCoverage(coverage).ToImmutableArray(), contributors, outcomes);
    }

    private static async Task<ImmutableArray<ResolutionOutcome>> LoadPartialOutcomesAsync(OctoberDbContext context,
        ResolutionCorpus corpus, ImmutableArray<Contributor> ledger, CancellationToken ct)
    {
        Guid batch = corpus.Handle.Binding.BatchId, corpusId = corpus.Handle.CorpusId;
        var plan = OctoberResolutionPolicy.Plan(corpus);
        var items = await context.ResolutionItems.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var dependencies = await context.ResolutionDocumentDependencies.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var edges = await context.RelationshipVersions.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        var unknowns = await context.UncertaintyVersions.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(ct);
        if (items.Count > plan.Inquiries.Length || items.Select(x => x.InquiryFingerprint).Distinct(StringComparer.Ordinal).Count() != items.Count ||
            items.Any(x => plan.Inquiries.All(i => i.Fingerprint != x.InquiryFingerprint)) ||
            dependencies.Any(x => items.All(i => i.ItemId != x.ItemId)) || edges.Any(x => items.All(i => i.ItemId != x.ItemId)) ||
            unknowns.Any(x => items.All(i => i.ItemId != x.ItemId))) throw new JsonException();
        var results = ImmutableArray.CreateBuilder<ResolutionOutcome>();
        foreach (var item in items.OrderBy(x => x.InquiryFingerprint, StringComparer.Ordinal))
        {
            var inquiry = plan.Inquiries.Single(x => x.Fingerprint == item.InquiryFingerprint);
            var outcome = Required<ResolutionOutcome>(item.OutcomeJson);
            if (item.ItemId != new Guid(Convert.FromHexString(inquiry.Fingerprint).AsSpan(0, 16)) || item.OperationKind != OperationKind.AcceptResolution ||
                item.PolicyVersion != OctoberResolutionPolicy.PolicyVersion || !SameSet(Required<ImmutableArray<Contributor>>(item.ContributorsJson), ledger) ||
                !SameSet(Required<ImmutableArray<ResolutionItemDisposition>>(item.PlanDispositionsJson), plan.Dispositions)) throw new JsonException();
            ResolutionOutcome expected;
            if (item.WitnessJson is not null)
                expected = OctoberResolutionPolicy.Validate(corpus, inquiry, Required<ResolutionWitness>(item.WitnessJson)) with { Contributors = ledger };
            else
            {
                if (outcome.Kind is not (ResolutionOutcomeKind.Failed or ResolutionOutcomeKind.NotEvaluated)) throw new JsonException();
                var caller = corpus.Documents.Single(x => x.Version == inquiry.Caller.Document).Observations.Single(x => x.ObservationKey == inquiry.Caller.ObservationKey);
                string reason = outcome.Kind == ResolutionOutcomeKind.Failed ? "The host inquiry failed; no connecting conclusion was accepted." : "The host did not evaluate this selected inquiry.";
                expected = new(inquiry.Fingerprint, outcome.Kind, null, new("resolution/" + inquiry.Fingerprint,
                    "Static linkage remains unresolved.", caller.Evidence, inquiry.Limitations.Add(reason)), ledger);
            }
            if (!Same(expected, outcome)) throw new JsonException();
            var links = dependencies.Where(x => x.ItemId == item.ItemId).ToArray();
            if (links.Length != corpus.Documents.Length || !SameSet(links.Select(x => new DocumentVersionReference(batch, x.LogicalDocumentId, x.VersionId)), corpus.Documents.Select(x => x.Version)) ||
                links.Count(x => x.IsInquiryCaller) != 1 || !links.Any(x => x.IsInquiryCaller && x.LogicalDocumentId == inquiry.Caller.Document.LogicalDocumentId && x.VersionId == inquiry.Caller.Document.DocumentVersionId)) throw new JsonException();
            var itemEdges = edges.Where(x => x.ItemId == item.ItemId).ToArray();
            if (itemEdges.Length != (outcome.Relationship is null ? 0 : 1)) throw new JsonException();
            if (outcome.Relationship is { } edge)
            {
                var stored = itemEdges[0];
                if (stored.RelationshipVersionId != edge.RelationshipVersionId || stored.FromLogicalDocumentId != edge.From.LogicalDocumentId ||
                    stored.FromVersionId != edge.From.DocumentVersionId || stored.ToLogicalDocumentId != edge.To.LogicalDocumentId || stored.ToVersionId != edge.To.DocumentVersionId ||
                    !Same(Required<AcceptedRelationship>(stored.ContentJson), edge)) throw new JsonException();
            }
            var itemUnknowns = unknowns.Where(x => x.ItemId == item.ItemId).ToArray();
            if (itemUnknowns.Length != (outcome.Uncertainty is null ? 0 : 1)) throw new JsonException();
            if (outcome.Uncertainty is { } unknown)
            {
                Guid itemId = item.ItemId;
                var id = new Guid(Convert.FromHexString(OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(
                    new { domain = "october/resolution-uncertainty/1", itemId, unknown.Key }))).AsSpan(0, 16));
                if (itemUnknowns[0].UncertaintyId != id || itemUnknowns[0].Key != unknown.Key || !Same(Required<Uncertainty>(itemUnknowns[0].ContentJson), unknown)) throw new JsonException();
            }
            var operation = await FindAsync(context, new(batch, item.OperationKind, item.OperationId), ct);
            if (operation is null || !SameSet(OctoberKnowledgeOperationStore.Contributors(operation), ledger)) throw new JsonException();
            var receipt = OctoberKnowledgeOperationStore.Result(operation);
            if (receipt.Status != KnowledgeWriteStatus.Committed || receipt.Corpus != corpus.Handle || receipt.Operation != new OperationKey(batch, item.OperationKind, item.OperationId)) throw new JsonException();
            results.Add(outcome);
        }
        return results.ToImmutable();
    }

    private static FileCoverage Coverage(AcceptedFileManifest manifest) => new(manifest.AdmissionId,
        new(manifest.RepositoryId, manifest.RootBindingIdentity, manifest.RelativePath, manifest.SourceDigest, manifest.PreparedVersion),
        manifest.CoverageDisposition, manifest.CoverageReason);

    private bool Owns(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission, out Admission? owner)
    {
        owner = admission as Admission;
        if (owner is null || !ReferenceEquals(owner.Issuer, this) || !owner.Committed ||
            !ReferenceEquals(owner.Request, request.Begin) || !Valid(request.Begin) ||
            request.CanonicalPayloadDigest != OctoberBusinessFromClPolicy.ComputeCompletionDigest(request)) return false;
        lock (_sync) return _owners.TryGetValue(OctoberBusinessFromClPolicy.BeginOperation(request.Begin), out var retained) && ReferenceEquals(retained, owner);
    }
    private BusinessFromClAdmission? Capability(Admission? owner) => owner is { Committed: true } && ReferenceEquals(owner.Issuer, this) ? owner : null;
    private async Task<KnowledgeWriteStatus?> VerifySettlementAsync(BusinessFromClCompletionRequest request, Admission owner, CancellationToken token)
    {
        if (!Owns(request, owner, out _)) return KnowledgeWriteStatus.Denied;
        try
        {
            await using var context = await OpenAsync(token);
            if (await context.KnowledgeOperations.AsNoTracking().AnyAsync(x => x.BatchId == request.Begin.Publication.Knowledge.BatchId &&
                x.OperationId == request.Begin.RunId && x.Kind == OperationKind.AbandonBusinessFromCl, token))
                return KnowledgeWriteStatus.StaleOrFenced;
            return await _settlement.VerifyAsync(request, owner, token).WaitAsync(token) switch
            {
                BusinessFromClSettlement.Settled => null,
                BusinessFromClSettlement.Unsettled => KnowledgeWriteStatus.CommitIndeterminate,
                _ => KnowledgeWriteStatus.AuthorityUnavailable
            };
        }
        catch (Exception) { return KnowledgeWriteStatus.AuthorityUnavailable; }
    }

    public async Task<CommitEligibilityStatus> CheckActiveAsync(BusinessFromClRequest request, BusinessFromClAdmission admission,
        CancellationToken cancellationToken = default)
    {
        var completion = new BusinessFromClCompletionRequest(request, "");
        completion = completion with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeCompletionDigest(completion) };
        if (!Owns(completion, admission, out _)) return CommitEligibilityStatus.Denied;
        try
        {
            await using var context = await OpenAsync(cancellationToken);
            bool terminal = await context.KnowledgeOperations.AsNoTracking().AnyAsync(x =>
                x.BatchId == request.Publication.Knowledge.BatchId && x.OperationId == request.RunId &&
                (x.Kind == OperationKind.AbandonBusinessFromCl || x.Kind == OperationKind.CompleteBusinessFromCl), cancellationToken);
            return terminal ? CommitEligibilityStatus.StaleOrFenced : CommitEligibilityStatus.Eligible;
        }
        catch (OperationCanceledException) { return CommitEligibilityStatus.Cancelled; }
        catch (Exception) { return CommitEligibilityStatus.AuthorityUnavailable; }
    }
    private async Task AuthorizeAsync(BusinessFromClRequest request, ImmutableArray<Contributor> contributors, CancellationToken token)
    {
        DisclosureStatus status;
        try { status = await _authority.CheckAsync(request, contributors, token).WaitAsync(token); }
        catch (Exception) { throw new AuthorityFailure(KnowledgeWriteStatus.AuthorityUnavailable); }
        token.ThrowIfCancellationRequested();
        if (status != DisclosureStatus.Allowed) throw new AuthorityFailure(status == DisclosureStatus.Denied ? KnowledgeWriteStatus.Denied : KnowledgeWriteStatus.AuthorityUnavailable);
    }
    private static bool Valid(BusinessFromClRequest request)
    {
        try { return request.RunId != Guid.Empty && request.Publication.PublicationId != Guid.Empty && request.Publication.Knowledge.BatchId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(request.SubjectId) && request.CanonicalPayloadDigest == OctoberBusinessFromClPolicy.ComputeBeginDigest(request); }
        catch (Exception) { return false; }
    }
    private static T Required<T>(string json) => JsonSerializer.Deserialize<T>(json, Json) ?? throw new JsonException();
    private static bool Same<T>(T a, T b) => OctoberKnowledgePolicy.SerializeCanonical(a) == OctoberKnowledgePolicy.SerializeCanonical(b);
    private static bool SameSet<T>(IEnumerable<T> a, IEnumerable<T> b) => a.Select(OctoberKnowledgePolicy.SerializeCanonical).ToHashSet(StringComparer.Ordinal)
        .SetEquals(b.Select(OctoberKnowledgePolicy.SerializeCanonical));
    private static async Task<KnowledgeOperation?> FindAsync(OctoberDbContext context, OperationKey operation, CancellationToken ct)
    {
        var query = context.KnowledgeOperations.AsNoTracking().Where(x => x.BatchId == operation.BatchId && x.Kind == operation.Kind && x.OperationId == operation.OperationId);
        var budget = new OctoberKnowledgeStoredReadBudget();
        if (context.Database.IsNpgsql())
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContributorsJson"::text)::bigint + octet_length("CommittedResultJson"::text) AS "Value"
                FROM october."KnowledgeOperations" WHERE "BatchId"={operation.BatchId} AND "Kind"={(int)operation.Kind}
                    AND "OperationId"={operation.OperationId} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
        else budget.Add(await query.Select(x => (long)x.ContributorsJson.Length + x.CommittedResultJson.Length).Take(2).ToListAsync(ct), 1);
        return await query.SingleOrDefaultAsync(ct);
    }
    private async Task<OctoberDbContext> OpenAsync(CancellationToken ct)
    {
        var task = _contexts.CreateDbContextAsync(ct);
        try { return await task.WaitAsync(ct); }
        catch { _ = DisposeLateAsync(task); throw; }
    }
    private static async Task DisposeLateAsync(Task<OctoberDbContext> task) { try { await (await task).DisposeAsync(); } catch (Exception) { } }

    private sealed class AuthorityFailure(KnowledgeWriteStatus status) : Exception { internal KnowledgeWriteStatus Status { get; } = status; }
    private sealed class Admission(OctoberBusinessFromClStore issuer, BusinessFromClRequest request) : BusinessFromClAdmission
    {
        internal OctoberBusinessFromClStore Issuer { get; } = issuer;
        internal BusinessFromClRequest Request { get; } = request;
        internal Task<BusinessFromClResult>? BeginTask;
        internal volatile bool Submitted, Committed;
        internal CorpusHandle? Corpus;
    }
    private sealed class RequestAuthorization(OctoberBusinessFromClStore owner, BusinessFromClRequest request) : IOctoberKnowledgeProducerAuthorization
    {
        public async Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
        {
            if (binding != request.Publication.Knowledge) return DisclosureStatus.Denied;
            try { await owner.AuthorizeAsync(request, contributors, cancellationToken); return DisclosureStatus.Allowed; }
            catch (AuthorityFailure failure) { return failure.Status == KnowledgeWriteStatus.Denied ? DisclosureStatus.Denied : DisclosureStatus.AuthorityUnavailable; }
        }
    }
    private sealed class CleanupAuthorization(OctoberBusinessFromClStore store, BusinessFromClCompletionRequest request, Admission owner) : IOctoberKnowledgeProducerAuthorization
    {
        public Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default) =>
            Task.FromResult(binding == request.Begin.Publication.Knowledge && contributors.IsEmpty && store.Owns(request, owner, out _)
                ? DisclosureStatus.Allowed : DisclosureStatus.Denied);
    }
    private sealed class BeginGuard(IOctoberCommitSubmissionGuard inner, Admission owner) : IOctoberCommitSubmissionGuard
    {
        public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default) => inner.CheckBeforeTransactionAsync(attempt, cancellationToken);
        public Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default) =>
            inner.SubmitAsync(attempt, ct => { var submitted = submitAsync(ct); owner.Submitted = true; return submitted; }, cancellationToken);
    }
    private sealed class CompletionGuard(OctoberBusinessFromClStore store, BusinessFromClCompletionRequest request, Admission owner,
        IOctoberCommitSubmissionGuard inner) : IOctoberCommitSubmissionGuard
    {
        public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default) => inner.CheckBeforeTransactionAsync(attempt, cancellationToken);
        public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
        {
            if (await store.VerifySettlementAsync(request, owner, cancellationToken) is not null) return new(CommitSubmissionStatus.StaleOrFenced, null);
            return await inner.SubmitAsync(attempt, submitAsync, cancellationToken);
        }
    }
}
