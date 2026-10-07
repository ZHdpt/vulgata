using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Accepts policy-derived outcomes from a pinned corpus; proposals never directly mint persisted edges.</summary>
public sealed class OctoberResolutionStore : IOctoberResolutionStore
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<OctoberDbContext> _contexts;
    private readonly OctoberKnowledgePublicationCommitter _committer;
    private readonly IOctoberKnowledgeOperationLog _log;

    public OctoberResolutionStore(IDbContextFactory<OctoberDbContext> contexts, IOctoberKnowledgeProducerAuthorization authorization,
        IOctoberCommitSubmissionGuard guard, IOctoberKnowledgeOperationLog log)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _committer = new(contexts, authorization, guard, log);
    }

    public static string ComputeAcceptanceDigest(ResolutionAcceptanceRequest request) =>
        OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/resolution-accept/1", schemaVersion = 1, request.Operation, request.Corpus, request.AttemptId,
            request.InquiryFingerprint, request.Witness, request.ExecutionDisposition
        }));

    public async Task<ResolutionCorpusResult> LoadCorpusAsync(CorpusHandle corpus, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await using var context = await _contexts.CreateDbContextAsync(deadline.Token);
            var retained = await FindCorpusAsync(context, corpus, deadline.Token);
            if (retained is null) return new(KnowledgeReadStatus.Unavailable, null);
            var contributors = OctoberCorpusSelectionStore.DeserializeContributors(retained.ContributorsJson);
            if (await _committer.AuthorizeAsync(corpus.Binding, contributors, deadline.Token) != DisclosureStatus.Allowed)
                return new(KnowledgeReadStatus.Unavailable, null);
            if (retained.CorpusDigest != corpus.CorpusDigest) return new(KnowledgeReadStatus.Unavailable, null);
            var result = await LoadExactCorpusAsync(context, corpus, deadline.Token);
            if (await _committer.AuthorizeAsync(corpus.Binding, contributors, deadline.Token) != DisclosureStatus.Allowed)
                return new(KnowledgeReadStatus.Unavailable, null);
            return new(KnowledgeReadStatus.Success, result);
        }
        catch (Exception) { return new(KnowledgeReadStatus.Unavailable, null); }
    }

    public async Task<ResolutionAcceptanceResult> AcceptOutcomeAsync(ResolutionAcceptanceRequest request, CancellationToken cancellationToken = default)
    {
        if (request.AttemptId == Guid.Empty || request.Operation.Kind != OperationKind.AcceptResolution)
            return new(KnowledgeWriteStatus.InvalidProposal, request.Operation, null);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var result = await _committer.ExecuteAsync(request.Corpus.Binding, request.Operation, OperationKind.AcceptResolution,
            request.CanonicalPayloadDigest, () => ComputeAcceptanceDigest(request), _ => Task.FromResult<KnowledgeWriteStatus?>(null),
            (context, token) => PrepareAsync(context, request, token), deadline.Token, request.AttemptId);
        if (result.Status == KnowledgeWriteStatus.CommitIndeterminate && deadline.IsCancellationRequested)
            return new(KnowledgeWriteStatus.CommitIndeterminate, request.Operation, null);
        if (result.Status is not (KnowledgeWriteStatus.Committed or KnowledgeWriteStatus.AlreadyCommitted or KnowledgeWriteStatus.LogUnavailable or KnowledgeWriteStatus.CommitIndeterminate))
            return new(result.Status, request.Operation, null);
        // A fresh bounded lookup is storage reconciliation only; it never repeats the inquiry or regenerates a witness.
        using var reconciliation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var found = await LookupCoreAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest, false, reconciliation.Token);
        if (found.Status == KnowledgeWriteStatus.AlreadyCommitted)
            return found with { Status = result.Status == KnowledgeWriteStatus.CommitIndeterminate ? KnowledgeWriteStatus.AlreadyCommitted : result.Status };
        if (found.Status == KnowledgeWriteStatus.CommitIndeterminate)
        {
            try
            {
                await using var context = await _contexts.CreateDbContextAsync(reconciliation.Token);
                var winner = await context.ResolutionItems.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == request.Corpus.Binding.BatchId &&
                    x.CorpusId == request.Corpus.CorpusId && x.InquiryFingerprint == request.InquiryFingerprint, reconciliation.Token);
                if (winner is not null && winner.OperationId != request.Operation.OperationId)
                {
                    var authority = await _committer.AuthorizeAsync(request.Corpus.Binding,
                        OctoberCorpusSelectionStore.DeserializeContributors(winner.ContributorsJson), reconciliation.Token);
                    return new(authority == DisclosureStatus.Allowed ? KnowledgeWriteStatus.Conflict : OctoberKnowledgePublicationCommitter.AuthorityStatus(authority), request.Operation, null);
                }
            }
            catch (Exception) { /* A temporary absence/outage cannot establish rollback. */ }
        }
        return found;
    }

    public async Task<ResolutionAcceptanceResult> LookupOutcomeAsync(CorpusHandle corpus, OperationKey operation, string payloadDigest,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        return await LookupCoreAsync(corpus, operation, payloadDigest, true, deadline.Token);
    }

    private async Task<ResolutionAcceptanceResult> LookupCoreAsync(CorpusHandle corpus, OperationKey operation, string digest, bool log, CancellationToken ct)
    {
        if (operation.Kind != OperationKind.AcceptResolution || operation.BatchId != corpus.Binding.BatchId || operation.OperationId == Guid.Empty)
            return new(KnowledgeWriteStatus.InvalidProposal, operation, null);
        try
        {
            await using var context = await _contexts.CreateDbContextAsync(ct);
            if (await FindCorpusAsync(context, corpus, ct) is not { } retained) return new(KnowledgeWriteStatus.Denied, operation, null);
            var recorded = await context.KnowledgeOperations.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == operation.BatchId && x.Kind == operation.Kind && x.OperationId == operation.OperationId, ct);
            if (recorded is null) return new(KnowledgeWriteStatus.CommitIndeterminate, operation, null);
            var contributors = OctoberKnowledgeOperationStore.Contributors(recorded);
            var authority = await _committer.AuthorizeAsync(corpus.Binding, contributors, ct);
            if (authority != DisclosureStatus.Allowed) return new(OctoberKnowledgePublicationCommitter.AuthorityStatus(authority), operation, null);
            var item = await context.ResolutionItems.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == operation.BatchId && x.OperationKind == operation.Kind && x.OperationId == operation.OperationId, ct);
            if (item is null) return new(KnowledgeWriteStatus.CommitIndeterminate, operation, null);
            if (item.CorpusId != corpus.CorpusId || retained.CorpusDigest != corpus.CorpusDigest || recorded.PayloadDigest != digest)
                return new(KnowledgeWriteStatus.Conflict, operation, null);
            var outcome = Required<ResolutionOutcome>(item.OutcomeJson);
            bool logged = true;
            if (log)
            {
                try { logged = await _log.AppendAsync(new(corpus.Binding, operation, KnowledgeWriteStatus.AlreadyCommitted), ct).WaitAsync(ct); }
                catch (Exception) { logged = false; }
            }
            authority = await _committer.AuthorizeAsync(corpus.Binding, contributors, ct);
            if (authority != DisclosureStatus.Allowed) return new(OctoberKnowledgePublicationCommitter.AuthorityStatus(authority), operation, null);
            return new(logged ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.LogUnavailable, operation, outcome);
        }
        catch (Exception) { return new(KnowledgeWriteStatus.CommitIndeterminate, operation, null); }
    }

    private async Task<PreparedPublicationMutation> PrepareAsync(OctoberDbContext context, ResolutionAcceptanceRequest request, CancellationToken ct)
    {
        var retained = await FindCorpusAsync(context, request.Corpus, ct);
        if (retained is null) return Failure(request, KnowledgeWriteStatus.InvalidProposal);
        var contributors = OctoberCorpusSelectionStore.DeserializeContributors(retained.ContributorsJson);
        var authority = await _committer.AuthorizeAsync(request.Corpus.Binding, contributors, ct);
        if (authority != DisclosureStatus.Allowed) return Failure(request, OctoberKnowledgePublicationCommitter.AuthorityStatus(authority));
        if (retained.CorpusDigest != request.Corpus.CorpusDigest) return Failure(request, KnowledgeWriteStatus.InvalidProposal);
        var existing = await context.ResolutionItems.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == request.Corpus.Binding.BatchId &&
            x.CorpusId == request.Corpus.CorpusId && x.InquiryFingerprint == request.InquiryFingerprint, ct);
        if (existing is not null)
        {
            authority = await _committer.AuthorizeAsync(request.Corpus.Binding, OctoberCorpusSelectionStore.DeserializeContributors(existing.ContributorsJson), ct);
            return Failure(request, authority == DisclosureStatus.Allowed ? KnowledgeWriteStatus.Conflict : OctoberKnowledgePublicationCommitter.AuthorityStatus(authority));
        }
        var corpus = await LoadExactCorpusAsync(context, request.Corpus, ct);
        var plan = OctoberResolutionPolicy.Plan(corpus);
        var inquiry = plan.Inquiries.SingleOrDefault(x => x.Fingerprint == request.InquiryFingerprint);
        if (inquiry is null || request.Witness is not null && request.ExecutionDisposition is not null ||
            request.Witness is null && request.ExecutionDisposition is not (ResolutionOutcomeKind.Failed or ResolutionOutcomeKind.NotEvaluated))
            return Failure(request, KnowledgeWriteStatus.InvalidProposal);
        ResolutionOutcome outcome;
        if (request.Witness is { } witness) outcome = OctoberResolutionPolicy.Validate(corpus, inquiry, witness);
        else
        {
            var caller = corpus.Documents.Single(x => x.Version == inquiry.Caller.Document).Observations.Single(x => x.ObservationKey == inquiry.Caller.ObservationKey);
            string limitation = request.ExecutionDisposition == ResolutionOutcomeKind.Failed ? "The host inquiry failed; no connecting conclusion was accepted." : "The host did not evaluate this selected inquiry.";
            outcome = new(inquiry.Fingerprint, request.ExecutionDisposition!.Value, null,
                new("resolution/" + inquiry.Fingerprint, "Static linkage remains unresolved.", caller.Evidence,
                    inquiry.Limitations.Add(limitation)), contributors);
        }
        outcome = outcome with { Contributors = contributors };
        Guid itemId = new(Convert.FromHexString(inquiry.Fingerprint).AsSpan(0, 16));
        context.ResolutionItems.Add(new()
        {
            BatchId = request.Corpus.Binding.BatchId, CorpusId = request.Corpus.CorpusId, ItemId = itemId,
            OperationId = request.Operation.OperationId, InquiryFingerprint = inquiry.Fingerprint, PolicyVersion = OctoberResolutionPolicy.PolicyVersion,
            WitnessJson = request.Witness is null ? null : OctoberKnowledgePolicy.SerializeCanonical(request.Witness),
            OutcomeJson = OctoberKnowledgePolicy.SerializeCanonical(outcome), ContributorsJson = OctoberKnowledgePolicy.SerializeCanonical(contributors),
            PlanDispositionsJson = OctoberKnowledgePolicy.SerializeCanonical(plan.Dispositions), CreatedAtUtc = DateTimeOffset.UtcNow
        });
        foreach (var document in corpus.Documents)
            context.ResolutionDocumentDependencies.Add(new()
            {
                BatchId = request.Corpus.Binding.BatchId, CorpusId = request.Corpus.CorpusId, ItemId = itemId,
                LogicalDocumentId = document.Version.LogicalDocumentId, VersionId = document.Version.DocumentVersionId,
                IsInquiryCaller = document.Version == inquiry.Caller.Document
            });
        if (outcome.Relationship is { } relationship)
            context.RelationshipVersions.Add(new()
            {
                BatchId = request.Corpus.Binding.BatchId, CorpusId = request.Corpus.CorpusId, ItemId = itemId,
                RelationshipVersionId = relationship.RelationshipVersionId, FromLogicalDocumentId = relationship.From.LogicalDocumentId,
                FromVersionId = relationship.From.DocumentVersionId, ToLogicalDocumentId = relationship.To.LogicalDocumentId,
                ToVersionId = relationship.To.DocumentVersionId, ContentJson = OctoberKnowledgePolicy.SerializeCanonical(relationship)
            });
        if (outcome.Uncertainty is { } uncertainty)
            context.UncertaintyVersions.Add(new()
            {
                BatchId = request.Corpus.Binding.BatchId, CorpusId = request.Corpus.CorpusId, ItemId = itemId,
                UncertaintyId = new Guid(Convert.FromHexString(OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new { domain = "october/resolution-uncertainty/1", itemId, uncertainty.Key }))).AsSpan(0, 16)),
                Key = uncertainty.Key, ContentJson = OctoberKnowledgePolicy.SerializeCanonical(uncertainty)
            });
        return new(new(KnowledgeWriteStatus.Committed, request.Operation, Corpus: request.Corpus), contributors);
    }

    internal static async Task<CorpusSelectionRecord?> FindCorpusAsync(OctoberDbContext context, CorpusHandle corpus, CancellationToken ct)
    {
        var binding = corpus.Binding;
        if (!await context.Batches.AsNoTracking().AnyAsync(x => x.BatchId == binding.BatchId && x.TargetId == binding.TargetId &&
            x.CanonicalDatabaseIdentity == binding.CanonicalDatabaseIdentity && x.SelectionDigest == binding.SelectionDigest, ct)) return null;
        return await context.CorpusSelections.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == binding.BatchId && x.CorpusId == corpus.CorpusId, ct);
    }

    internal static async Task<ResolutionCorpus> LoadExactCorpusAsync(OctoberDbContext context, CorpusHandle corpus, CancellationToken ct,
        OctoberKnowledgeStoredReadBudget? sharedBudget = null)
    {
        Guid batch = corpus.Binding.BatchId;
        Guid corpusId = corpus.CorpusId;
        var budget = sharedBudget ?? new OctoberKnowledgeStoredReadBudget();
        var query = from member in context.CorpusDocuments.AsNoTracking()
                    join version in context.DocumentVersions.AsNoTracking() on new { member.BatchId, member.VersionId, member.LogicalDocumentId }
                        equals new { version.BatchId, version.VersionId, version.LogicalDocumentId }
                    join logical in context.LogicalDocuments.AsNoTracking() on new { member.BatchId, member.LogicalDocumentId }
                        equals new { logical.BatchId, logical.LogicalDocumentId }
                    join unit in context.CodeUnits.AsNoTracking() on new { logical.BatchId, logical.UnitId } equals new { unit.BatchId, UnitId = (Guid?)unit.UnitId }
                    where member.BatchId == batch && member.CorpusId == corpusId
                    select new { Version = version, logical.Kind, unit.UnitId, unit.RepositoryId };
        var manifests = await context.CorpusManifests.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => x.ManifestId).ToArrayAsync(ct);
        var observations = context.CommunicationObservations.AsNoTracking().Where(x => x.BatchId == batch && manifests.Contains(x.ManifestId));
        var contributors = context.KnowledgeContributors.AsNoTracking().Where(x => x.BatchId == batch && manifests.Contains(x.ManifestId));
        if (context.Database.IsNpgsql())
        {
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(v."ContentJson"::text)::bigint AS "Value" FROM october."OctoberCorpusDocuments" m
                JOIN october."OctoberDocumentVersions" v ON v."BatchId"=m."BatchId" AND v."VersionId"=m."VersionId"
                WHERE m."BatchId"={batch} AND m."CorpusId"={corpusId} LIMIT 10001
                """).ToListAsync(ct), bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(o."ContentJson"::text)::bigint AS "Value" FROM october."OctoberCommunicationObservations" o
                JOIN october."OctoberCorpusManifests" m ON m."BatchId"=o."BatchId" AND m."ManifestId"=o."ManifestId"
                WHERE m."BatchId"={batch} AND m."CorpusId"={corpusId} LIMIT 10001
                """).ToListAsync(ct), bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(c."ContributorJson"::text)::bigint AS "Value" FROM october."KnowledgeContributors" c
                JOIN october."OctoberCorpusManifests" m ON m."BatchId"=c."BatchId" AND m."ManifestId"=c."ManifestId"
                WHERE m."BatchId"={batch} AND m."CorpusId"={corpusId} LIMIT 100001
                """).ToListAsync(ct), OctoberKnowledgeStoredReadBudget.MaximumContributors, bytes: true);
        }
        else
        {
            budget.Add(await query.Select(x => (long)x.Version.ContentJson.Length).Take(10001).ToListAsync(ct));
            budget.Add(await observations.Select(x => (long)x.ContentJson.Length).Take(10001).ToListAsync(ct));
            budget.Add(await contributors.Select(x => (long)x.ContributorJson.Length).Take(100001).ToListAsync(ct), OctoberKnowledgeStoredReadBudget.MaximumContributors);
        }
        var versions = await query.ToListAsync(ct);
        var observed = await observations.ToListAsync(ct);
        var contributed = await contributors.OrderBy(x => x.Ordinal).ToListAsync(ct);
        var result = ImmutableArray.CreateBuilder<ResolutionDocument>();
        foreach (var entry in versions)
        {
            var version = entry.Version;
            string content = OctoberKnowledgePolicy.CanonicalizeJson(version.ContentJson);
            if (entry.Kind != DocumentKind.CodeLogic || version.SchemaVersion != KnowledgeContract.SchemaVersion || OctoberKnowledgePolicy.Hash(content) != version.ContentDigest)
                throw new JsonException("Resolution requires exact accepted CL content.");
            using var document = JsonDocument.Parse(content);
            var sections = document.RootElement.GetProperty("sections").Deserialize<ImmutableArray<DocumentSection>>(_json);
            if (sections.IsDefaultOrEmpty) throw new JsonException("Missing accepted sections.");
            result.Add(new(new(batch, version.LogicalDocumentId, version.VersionId), entry.RepositoryId, sections,
                observed.Where(x => x.ManifestId == version.ManifestId && x.UnitId == entry.UnitId).Select(x => Required<CommunicationObservation>(x.ContentJson)).ToImmutableArray(),
                contributed.Where(x => x.ManifestId == version.ManifestId).Select(x => Required<Contributor>(x.ContributorJson)).ToImmutableArray()));
        }
        return new(corpus, result.ToImmutable());
    }

    internal static T Required<T>(string json) where T : class => JsonSerializer.Deserialize<T>(json, _json) ?? throw new JsonException("Missing resolution fact.");
    private static PreparedPublicationMutation Failure(ResolutionAcceptanceRequest request, KnowledgeWriteStatus status) => new(new(status, request.Operation), []);
}
