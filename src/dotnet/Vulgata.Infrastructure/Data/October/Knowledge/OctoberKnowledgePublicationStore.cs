using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed class OctoberKnowledgePublicationStore
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly OctoberKnowledgePublicationCommitter _committer;

    public OctoberKnowledgePublicationStore(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberKnowledgeProducerAuthorization authorization, IOctoberCommitSubmissionGuard guard, IOctoberKnowledgeOperationLog log)
    {
        _committer = new(contexts, authorization, guard, log);
    }

    public Task<KnowledgeWriteResult> PublishAsync(PublicationRequest request, CancellationToken cancellationToken = default) =>
        _committer.ExecuteAsync(request.Corpus.Binding, request.Operation, OperationKind.PublishSelection, request.CanonicalPayloadDigest,
            () => OctoberKnowledgePublicationPolicy.ComputePublicationDigest(request), _ => Task.FromResult<KnowledgeWriteStatus?>(null),
            (context, token) => PrepareAsync(context, request, token), cancellationToken);

    private async Task<PreparedPublicationMutation> PrepareAsync(OctoberDbContext context, PublicationRequest request, CancellationToken cancellationToken)
    {
        Guid batch = request.Corpus.Binding.BatchId;
        Guid corpusId = request.Corpus.CorpusId;
        var operation = request.Operation;
        var corpus = await context.CorpusSelections.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batch && x.CorpusId == corpusId, cancellationToken);
        if (corpus is null) return Failure(operation);
        var contributors = OctoberCorpusSelectionStore.DeserializeContributors(corpus.ContributorsJson);
        var authority = await _committer.AuthorizeAsync(request.Corpus.Binding, contributors, cancellationToken);
        if (authority != DisclosureStatus.Allowed)
            return new(new(OctoberKnowledgePublicationCommitter.AuthorityStatus(authority), operation), []);
        if (corpus.CorpusDigest != request.Corpus.CorpusDigest || request.Documents.IsDefaultOrEmpty ||
            request.Relationships.IsDefault || !request.Relationships.IsEmpty || request.Flows.IsDefault || !request.Flows.IsEmpty ||
            request.Uncertainties.IsDefault || request.Coverage.IsDefault ||
            request.Documents.Any(x => x.BatchId != batch || x.DocumentVersionId == Guid.Empty || x.LogicalDocumentId == Guid.Empty) ||
            request.Documents.Select(x => x.LogicalDocumentId).Distinct().Count() != request.Documents.Length ||
            request.Documents.Select(x => x.DocumentVersionId).Distinct().Count() != request.Documents.Length)
            return Failure(operation);
        var documents = await context.CorpusDocuments.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(cancellationToken);
        var logicalIds = request.Documents.Select(x => x.LogicalDocumentId).ToArray();
        var kinds = await context.LogicalDocuments.AsNoTracking().Where(x => x.BatchId == batch && logicalIds.Contains(x.LogicalDocumentId)).ToListAsync(cancellationToken);
        if (request.Documents.Any(x => !documents.Any(d => d.LogicalDocumentId == x.LogicalDocumentId && d.VersionId == x.DocumentVersionId)) ||
            kinds.Count != request.Documents.Length || kinds.Any(x => x.Kind != DocumentKind.CodeLogic))
            return Failure(operation);
        var coverage = await context.CorpusCoverage.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(cancellationToken);
        if (request.Coverage.Length != coverage.Count || request.Coverage.Select(x => x.AdmissionId).Distinct().Count() != request.Coverage.Length ||
            request.Coverage.Any(x => !coverage.Any(c => c.AdmissionId == x.AdmissionId &&
                OctoberKnowledgePublicationPolicy.SameCoverage(x, JsonSerializer.Deserialize<FileCoverage>(c.CoverageJson, _json)!))))
            return Failure(operation);
        var manifestIds = await context.CorpusManifests.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).Select(x => x.ManifestId).ToListAsync(cancellationToken);
        var acceptedUnknowns = await context.KnowledgeUncertainties.AsNoTracking().Where(x => x.BatchId == batch && manifestIds.Contains(x.ManifestId)).ToListAsync(cancellationToken);
        var selectedUnknowns = new List<KnowledgeUncertaintyRecord>();
        foreach (var unknown in request.Uncertainties)
        {
            string canonical = OctoberKnowledgePolicy.SerializeCanonical(unknown);
            var exact = acceptedUnknowns.Where(x => OctoberKnowledgePolicy.CanonicalizeJson(x.ContentJson) == canonical).ToArray();
            // Shared uncertainty requests contain no manifest ID. Ambiguous identical payloads cannot choose an owner.
            if (exact.Length != 1 || selectedUnknowns.Any(x => x.ManifestId == exact[0].ManifestId && x.Key == exact[0].Key))
                return Failure(operation);
            selectedUnknowns.Add(exact[0]);
        }
        Guid publicationId = Guid.NewGuid();
        var publication = new PublicationBinding(request.Corpus.Binding, publicationId);
        context.KnowledgePublications.Add(new()
        {
            BatchId = batch, PublicationId = publicationId, CorpusId = corpusId, CorpusDigest = corpus.CorpusDigest,
            OperationId = operation.OperationId, ContributorsJson = corpus.ContributorsJson, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var ordered = OctoberKnowledgePublicationPolicy.OrderedDocuments(request.Documents);
        for (int index = 0; index < ordered.Length; index++)
            context.PublicationMembers.Add(new()
            {
                BatchId = batch, PublicationId = publicationId, CorpusId = corpusId, LogicalDocumentId = ordered[index].LogicalDocumentId,
                VersionId = ordered[index].DocumentVersionId, Ordinal = index
            });
        foreach (var item in coverage)
            context.PublicationCoverage.Add(new()
            {
                BatchId = batch, PublicationId = publicationId, CorpusId = corpusId, AdmissionId = item.AdmissionId,
                RepositoryId = item.RepositoryId, CoverageJson = item.CoverageJson, ContributorsJson = item.ContributorsJson
            });
        foreach (var item in selectedUnknowns)
            context.PublicationUncertainties.Add(new()
            {
                BatchId = batch, PublicationId = publicationId, CorpusId = corpusId, ManifestId = item.ManifestId, Key = item.Key
            });
        return new(new(KnowledgeWriteStatus.Committed, operation, Publication: publication), contributors);
    }

    private static PreparedPublicationMutation Failure(OperationKey operation) => new(new(KnowledgeWriteStatus.InvalidProposal, operation), []);
}
