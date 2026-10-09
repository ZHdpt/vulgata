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
            request.Relationships.IsDefault || request.Flows.IsDefault || !request.Flows.IsEmpty ||
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
        bool requestsResolution = !request.Relationships.IsEmpty || request.Uncertainties.Any(unknown =>
            !acceptedUnknowns.Any(x => OctoberKnowledgePolicy.CanonicalizeJson(x.ContentJson) == OctoberKnowledgePolicy.SerializeCanonical(unknown)));
        var resolutionUnknowns = requestsResolution
            ? await context.UncertaintyVersions.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(cancellationToken)
            : new List<UncertaintyVersion>();
        var relationshipVersions = requestsResolution
            ? await context.RelationshipVersions.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(cancellationToken)
            : new List<RelationshipVersion>();
        var selectedUnknowns = new List<KnowledgeUncertaintyRecord>();
        var selectedResolutionUnknowns = new List<UncertaintyVersion>();
        var selectedRelationships = new List<RelationshipVersion>();
        foreach (var relationship in request.Relationships)
        {
            string canonical = OctoberKnowledgePolicy.SerializeCanonical(relationship);
            var exact = relationshipVersions.SingleOrDefault(x => x.RelationshipVersionId == relationship.RelationshipVersionId &&
                OctoberKnowledgePolicy.CanonicalizeJson(x.ContentJson) == canonical);
            if (exact is null || selectedRelationships.Any(x => x.RelationshipVersionId == exact.RelationshipVersionId) ||
                !request.Documents.Contains(relationship.From) || !request.Documents.Contains(relationship.To)) return Failure(operation);
            selectedRelationships.Add(exact);
        }
        foreach (var requested in request.Uncertainties.GroupBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal))
        {
            string canonical = requested.Key;
            var exact = acceptedUnknowns.Where(x => OctoberKnowledgePolicy.CanonicalizeJson(x.ContentJson) == canonical).ToArray();
            var resolution = resolutionUnknowns.Where(x => OctoberKnowledgePolicy.CanonicalizeJson(x.ContentJson) == canonical).ToArray();
            // Without manifest IDs, identical file payloads can only select the full matching
            // multiset. A partial count cannot identify which immutable file owner was intended.
            if (exact.Length != 0)
            {
                if (resolution.Length != 0 || requested.Count() != exact.Length ||
                    exact.Any(match => selectedUnknowns.Any(x => x.ManifestId == match.ManifestId && x.Key == match.Key)))
                    return Failure(operation);
                selectedUnknowns.AddRange(exact);
            }
            else
            {
                // Resolution selection remains singular; no cross-origin or duplicate aliasing.
                if (resolution.Length != 1 || requested.Count() != 1 ||
                    selectedResolutionUnknowns.Any(x => x.UncertaintyId == resolution[0].UncertaintyId))
                    return Failure(operation);
                selectedResolutionUnknowns.Add(resolution[0]);
            }
        }
        if (selectedRelationships.Count != 0 || selectedResolutionUnknowns.Count != 0)
        {
            var actualCorpus = await OctoberResolutionStore.LoadExactCorpusAsync(context, request.Corpus, cancellationToken);
            var plan = OctoberResolutionPolicy.Plan(actualCorpus);
            var items = await context.ResolutionItems.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId).ToListAsync(cancellationToken);
            string dispositions = OctoberKnowledgePolicy.SerializeCanonical(plan.Dispositions);
            if (plan.Inquiries.Any(inquiry => !items.Any(item => item.InquiryFingerprint == inquiry.Fingerprint &&
                item.PolicyVersion == OctoberResolutionPolicy.PolicyVersion && OctoberKnowledgePolicy.CanonicalizeJson(item.PlanDispositionsJson) == dispositions)))
                return Failure(operation);
            // An accepted supported edge never sheds its own source/witness qualifications at publication.
            if (selectedRelationships.Any(edge => resolutionUnknowns.Any(unknown => unknown.ItemId == edge.ItemId &&
                !selectedResolutionUnknowns.Any(selected => selected.UncertaintyId == unknown.UncertaintyId)))) return Failure(operation);
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
        foreach (var item in selectedRelationships)
            context.PublicationRelationships.Add(new()
            {
                BatchId = batch, PublicationId = publicationId, CorpusId = corpusId, RelationshipVersionId = item.RelationshipVersionId,
                FromLogicalDocumentId = item.FromLogicalDocumentId, FromVersionId = item.FromVersionId,
                ToLogicalDocumentId = item.ToLogicalDocumentId, ToVersionId = item.ToVersionId
            });
        foreach (var item in selectedResolutionUnknowns)
            context.PublicationResolutionUncertainties.Add(new()
            {
                BatchId = batch, PublicationId = publicationId, CorpusId = corpusId, UncertaintyId = item.UncertaintyId
            });
        return new(new(KnowledgeWriteStatus.Committed, operation, Publication: publication), contributors);
    }

    private static PreparedPublicationMutation Failure(OperationKey operation) => new(new(KnowledgeWriteStatus.InvalidProposal, operation), []);
}
