using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

public sealed class OctoberCorpusSelectionStore
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly IOctoberCorpusFreezeAuthority _freezeAuthority;
    private readonly OctoberKnowledgePublicationCommitter _committer;

    public OctoberCorpusSelectionStore(IDbContextFactory<OctoberDbContext> contexts,
        IOctoberKnowledgeProducerAuthorization authorization, IOctoberCommitSubmissionGuard guard,
        IOctoberKnowledgeOperationLog log, IOctoberCorpusFreezeAuthority freezeAuthority)
    {
        _freezeAuthority = freezeAuthority ?? throw new ArgumentNullException(nameof(freezeAuthority));
        _committer = new(contexts, authorization, guard, log);
    }

    public Task<KnowledgeWriteResult> FreezeAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default) =>
        _committer.ExecuteAsync(request.Binding, request.Operation, OperationKind.FreezeCorpus, request.CanonicalPayloadDigest,
            () => OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(request),
            token => VerifyHostAsync(request, token), (context, token) => PrepareAsync(context, request, token), cancellationToken);

    private async Task<KnowledgeWriteStatus?> VerifyHostAsync(CorpusFreezeRequest request, CancellationToken cancellationToken)
    {
        CorpusFreezeEligibility eligibility;
        try { eligibility = await _freezeAuthority.VerifyAsync(request, cancellationToken).WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return KnowledgeWriteStatus.AuthorityUnavailable; }
        return eligibility switch
        {
            CorpusFreezeEligibility.Eligible => null,
            CorpusFreezeEligibility.Unsettled => KnowledgeWriteStatus.CommitIndeterminate,
            CorpusFreezeEligibility.AdmissionOpen or CorpusFreezeEligibility.Paused => KnowledgeWriteStatus.NotCompleted,
            CorpusFreezeEligibility.HardStopped => KnowledgeWriteStatus.StaleOrFenced,
            CorpusFreezeEligibility.Denied => KnowledgeWriteStatus.Denied,
            _ => KnowledgeWriteStatus.AuthorityUnavailable
        };
    }

    private async Task<PreparedPublicationMutation> PrepareAsync(OctoberDbContext context, CorpusFreezeRequest request, CancellationToken cancellationToken)
    {
        var operation = request.Operation;
        Guid batch = request.Binding.BatchId;
        var previous = await context.CorpusSelections.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batch, cancellationToken);
        if (previous is not null)
        {
            var priorContributors = DeserializeContributors(previous.ContributorsJson);
            var authority = await _committer.AuthorizeAsync(request.Binding, priorContributors, cancellationToken);
            return Failure(operation, authority == DisclosureStatus.Allowed ? KnowledgeWriteStatus.Conflict : OctoberKnowledgePublicationCommitter.AuthorityStatus(authority));
        }
        var manifests = await context.AcceptedFileManifests.AsNoTracking().Where(x => x.BatchId == batch).ToListAsync(cancellationToken);
        var versions = await context.DocumentVersions.AsNoTracking().Where(x => x.BatchId == batch).ToListAsync(cancellationToken);
        var contributorRows = await context.KnowledgeContributors.AsNoTracking().Where(x => x.BatchId == batch).ToListAsync(cancellationToken);
        var repositories = await context.RepositorySelections.AsNoTracking().Where(x => x.BatchId == batch).ToListAsync(cancellationToken);
        var acceptedContributors = contributorRows.Select(x => JsonSerializer.Deserialize<Contributor>(x.ContributorJson, _json)!).Distinct().ToImmutableArray();
        var acceptedAuthority = await _committer.AuthorizeAsync(request.Binding, acceptedContributors, cancellationToken);
        if (acceptedAuthority != DisclosureStatus.Allowed) return Failure(operation, OctoberKnowledgePublicationCommitter.AuthorityStatus(acceptedAuthority));
        if (request.AcceptedFiles.IsDefault || request.TerminalCoverage.IsDefault ||
            request.AcceptedFiles.Length != manifests.Count || request.AcceptedFiles.Select(x => x.ManifestId).Distinct().Count() != request.AcceptedFiles.Length ||
            request.AcceptedFiles.Select(x => x.AdmissionId).Distinct().Count() != request.AcceptedFiles.Length ||
            request.TerminalCoverage.Select(x => x.AdmissionId).Distinct().Count() != request.TerminalCoverage.Length ||
            request.TerminalCoverage.Any(x => !OctoberKnowledgePublicationPolicy.ValidCoverage(x)))
            return Failure(operation, KnowledgeWriteStatus.InvalidProposal);
        foreach (var supplied in request.AcceptedFiles)
        {
            var manifest = manifests.SingleOrDefault(x => x.ManifestId == supplied.ManifestId);
            if (manifest is null || manifest.AdmissionId != supplied.AdmissionId || manifest.ProposalDigest != supplied.ProposalDigest ||
                supplied.Documents.IsDefault || supplied.Documents.Distinct().Count() != supplied.Documents.Length ||
                !OctoberKnowledgePublicationPolicy.SameCoverage(Coverage(manifest), supplied.Coverage))
                return Failure(operation, KnowledgeWriteStatus.InvalidProposal);
            var exactVersions = versions.Where(x => x.ManifestId == manifest.ManifestId)
                .Select(x => new DocumentVersionReference(batch, x.LogicalDocumentId, x.VersionId)).ToArray();
            if (exactVersions.Length != manifest.UnitCount || !supplied.Documents.ToHashSet().SetEquals(exactVersions))
                return Failure(operation, KnowledgeWriteStatus.InvalidProposal);
            var terminal = request.TerminalCoverage.SingleOrDefault(x => x.AdmissionId == supplied.AdmissionId);
            if (terminal is null || !OctoberKnowledgePublicationPolicy.SameCoverage(terminal, supplied.Coverage))
                return Failure(operation, KnowledgeWriteStatus.InvalidProposal);
        }
        foreach (var coverage in request.TerminalCoverage)
        {
            if (!repositories.Any(x => x.RepositoryId == coverage.Origin.RepositoryId && x.RootBindingIdentity == coverage.Origin.RootBindingIdentity))
                return Failure(operation, KnowledgeWriteStatus.InvalidProposal);
            bool accepted = manifests.Any(x => x.AdmissionId == coverage.AdmissionId);
            if (accepted != (coverage.Disposition is CoverageDisposition.Accepted or CoverageDisposition.CompletedZeroUnit))
                return Failure(operation, KnowledgeWriteStatus.InvalidProposal);
        }
        // Host attestation owns the inventory; database reconciliation additionally proves that
        // every already-accepted manifest is pinned and no caller supplied a fabricated success.
        var coverageContributors = request.TerminalCoverage.ToDictionary(x => x.AdmissionId, x =>
        {
            var manifest = manifests.SingleOrDefault(m => m.AdmissionId == x.AdmissionId);
            if (manifest is not null)
                return contributorRows.Where(c => c.ManifestId == manifest.ManifestId)
                    .Select(c => JsonSerializer.Deserialize<Contributor>(c.ContributorJson, _json)!).Distinct().ToImmutableArray();
            var repository = repositories.Single(r => r.RepositoryId == x.Origin.RepositoryId);
            return ImmutableArray.Create(new Contributor(ContributorKind.Source, x.Origin.RepositoryId, repository.SystemId,
                x.Origin.RootBindingIdentity, null, x.Origin.SourceDigest));
        });
        var allContributors = acceptedContributors.Concat(coverageContributors.Values.SelectMany(x => x)).Distinct().ToImmutableArray();
        var allAuthority = await _committer.AuthorizeAsync(request.Binding, allContributors, cancellationToken);
        if (allAuthority != DisclosureStatus.Allowed) return Failure(operation, OctoberKnowledgePublicationCommitter.AuthorityStatus(allAuthority));
        Guid corpusId = Guid.NewGuid();
        var corpus = new CorpusHandle(request.Binding, corpusId, request.CanonicalPayloadDigest);
        context.CorpusSelections.Add(new()
        {
            BatchId = batch, CorpusId = corpusId, OperationId = operation.OperationId, CorpusDigest = corpus.CorpusDigest,
            ContributorsJson = OctoberKnowledgePolicy.SerializeCanonical(allContributors), CreatedAtUtc = DateTimeOffset.UtcNow
        });
        foreach (var manifest in manifests)
            context.CorpusManifests.Add(new() { BatchId = batch, CorpusId = corpusId, ManifestId = manifest.ManifestId });
        var orderedVersions = versions.OrderBy(x => x.LogicalDocumentId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(x => x.VersionId.ToString("D"), StringComparer.Ordinal).ToArray();
        for (int index = 0; index < orderedVersions.Length; index++)
        {
            var version = orderedVersions[index];
            context.CorpusDocuments.Add(new()
            {
                BatchId = batch, CorpusId = corpusId, ManifestId = version.ManifestId,
                LogicalDocumentId = version.LogicalDocumentId, VersionId = version.VersionId, Ordinal = index
            });
        }
        foreach (var coverage in OctoberKnowledgePublicationPolicy.OrderedCoverage(request.TerminalCoverage))
            context.CorpusCoverage.Add(new()
            {
                BatchId = batch, CorpusId = corpusId, AdmissionId = coverage.AdmissionId, RepositoryId = coverage.Origin.RepositoryId,
                CoverageJson = OctoberKnowledgePolicy.SerializeCanonical(coverage),
                ContributorsJson = OctoberKnowledgePolicy.SerializeCanonical(coverageContributors[coverage.AdmissionId])
            });
        return new(new(KnowledgeWriteStatus.Committed, operation, Corpus: corpus), allContributors);
    }

    private static FileCoverage Coverage(AcceptedFileManifest manifest) => new(manifest.AdmissionId,
        new(manifest.RepositoryId, manifest.RootBindingIdentity, manifest.RelativePath, manifest.SourceDigest, manifest.PreparedVersion),
        manifest.CoverageDisposition, manifest.CoverageReason);
    private static PreparedPublicationMutation Failure(OperationKey operation, KnowledgeWriteStatus status) => new(new(status, operation), []);
    internal static ImmutableArray<Contributor> DeserializeContributors(string json) => JsonSerializer.Deserialize<ImmutableArray<Contributor>>(json, _json);
}
