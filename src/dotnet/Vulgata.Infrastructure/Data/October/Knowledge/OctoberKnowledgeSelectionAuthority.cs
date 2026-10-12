using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Exact persisted setup provenance for one explicit publication; live management checks remain separate.</summary>
public sealed class OctoberKnowledgeSelectionAuthority(IDbContextFactory<OctoberDbContext> factory) : IOctoberKnowledgeSelectionAuthority
{
    private readonly IDbContextFactory<OctoberDbContext> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    public async Task<KnowledgeSelectionAuthoritySnapshot?> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        await using var context = await _factory.CreateDbContextAsync(token);
        if (!await OctoberKnowledgePublishedSource.IsSelectedAsync(context, publication, token)) return null;
        Guid batch = publication.Knowledge.BatchId;
        var budget = new OctoberKnowledgeStoredReadBudget();
        var repositoriesQuery = context.RepositorySelections.AsNoTracking().Where(x => x.BatchId == batch);
        budget.Add(await repositoriesQuery.Select(x => (long)x.RootBindingIdentity.Length)
            .Take(OctoberKnowledgeStoredReadBudget.MaximumItems + 1).ToListAsync(token));
        var repositories = await repositoriesQuery.Select(x => new KnowledgeRepositoryBinding(x.RepositoryId, x.SystemId, x.RootBindingIdentity)).ToListAsync(token);
        // All selected setup repositories remain valid explicit scope, including those with no admitted files.
        // Exact publication membership, not scope alone, determines which documents can be read.
        var advisoryQuery = context.AdvisoryEntries.AsNoTracking().Where(x => x.BatchId == batch && x.Applicability == 0);
        budget.Add(await advisoryQuery.Select(x => x.Value == null ? 0L : (long)x.Value.Length)
            .Take(OctoberKnowledgeStoredReadBudget.MaximumContributors + 1).ToListAsync(token), OctoberKnowledgeStoredReadBudget.MaximumContributors);
        var advisoryRows = await advisoryQuery.ToListAsync(token);
        if (advisoryRows.Any(x => x.Scope is < 0 or > 2 || x.EvidenceKind != 0 || x.OriginRecordId is null ||
            x.OriginRecordId == Guid.Empty || string.IsNullOrWhiteSpace(x.Value)))
            throw new InvalidOperationException("Invalid selected advisory provenance.");
        var advisory = advisoryRows.Select(x => new KnowledgeAdvisoryBinding(x.RepositoryId,
            (ContributorKind)(x.Scope + 1), x.OriginRecordId!.Value, OctoberKnowledgePolicy.Hash(x.Value!))).ToImmutableArray();
        ImmutableArray<KnowledgeAdvisoryBinding>? additional;
        try { additional = await LoadOverrideAsync(context, publication, repositories, advisoryRows, budget, token); }
        catch (ArgumentException) { return null; }
        catch (JsonException) { return null; }
        if (additional is null) return null;
        token.ThrowIfCancellationRequested();
        return new(publication, repositories.ToImmutableArray(), advisory.AddRange(additional.Value).Distinct().ToImmutableArray());
    }

    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    // Only the exact published finalization can authorize additional context versions.
    // Current management text and caller-supplied hashes are never reader authority.
    private static async Task<ImmutableArray<KnowledgeAdvisoryBinding>?> LoadOverrideAsync(
        OctoberDbContext context, PublicationBinding publication, IReadOnlyList<KnowledgeRepositoryBinding> repositories,
        IReadOnlyList<OctoberAdvisoryEntryRecord> advisoryRows, OctoberKnowledgeStoredReadBudget budget, CancellationToken ct)
    {
        Guid batch = publication.Knowledge.BatchId;
        Guid publicationId = publication.PublicationId;
        var query = context.BusinessFinalizations.AsNoTracking().Where(x => x.BatchId == batch && x.PublicationId == publicationId);
        if (context.Database.IsNpgsql())
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("RequestJson"::text)::bigint + octet_length("ContributorsJson"::text) AS "Value"
                FROM october."OctoberBusinessFinalizations" WHERE "BatchId"={batch} AND "PublicationId"={publicationId} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
        else
            budget.Add(await query.Select(x => (long)x.RequestJson.Length + x.ContributorsJson.Length).Take(2).ToListAsync(ct), 1);
        var stored = await query.Select(x => new { x.OperationId, x.OperationKind, x.CorpusId,
            x.RequestDigest, x.OutcomeDigest, x.RequestJson, x.ContributorsJson }).SingleOrDefaultAsync(ct);
        if (stored is null) return [];
        var request = JsonSerializer.Deserialize<FlowFinalizationPublicationRequest>(stored.RequestJson, _json);
        if (request is null || request.Selection is null || request.Operation is null || request.Corpus is null ||
            request.Corpus.Binding != publication.Knowledge || request.Corpus.CorpusId != stored.CorpusId ||
            request.Operation.BatchId != batch || request.Operation.Kind != OperationKind.FinalizeCorpus ||
            stored.OperationKind != OperationKind.FinalizeCorpus || request.Operation.OperationId != stored.OperationId ||
            request.CanonicalPayloadDigest != stored.RequestDigest || request.CanonicalOutcomeDigest != stored.OutcomeDigest ||
            OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) != stored.OutcomeDigest)
            return null;
        if (request.Selection.ContextOverride is null) return [];

        Guid corpusId = stored.CorpusId;
        var corpusQuery = context.CorpusSelections.AsNoTracking().Where(x => x.BatchId == batch && x.CorpusId == corpusId);
        var publicationQuery = context.KnowledgePublications.AsNoTracking().Where(x => x.BatchId == batch && x.PublicationId == publicationId);
        if (context.Database.IsNpgsql())
        {
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContributorsJson"::text)::bigint AS "Value"
                FROM october."OctoberCorpusSelections" WHERE "BatchId"={batch} AND "CorpusId"={corpusId} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("ContributorsJson"::text)::bigint AS "Value"
                FROM october."OctoberKnowledgePublications" WHERE "BatchId"={batch} AND "PublicationId"={publicationId} LIMIT 2
                """).ToListAsync(ct), 1, bytes: true);
        }
        else
        {
            budget.Add(await corpusQuery.Select(x => (long)x.ContributorsJson.Length).Take(2).ToListAsync(ct), 1);
            budget.Add(await publicationQuery.Select(x => (long)x.ContributorsJson.Length).Take(2).ToListAsync(ct), 1);
        }
        var corpus = await corpusQuery.Select(x => new { x.CorpusDigest, x.ContributorsJson }).SingleOrDefaultAsync(ct);
        var published = await publicationQuery.Select(x => new { x.CorpusId, x.CorpusDigest, x.OperationKind, x.OperationId, x.ContributorsJson }).SingleOrDefaultAsync(ct);
        if (corpus is null || published is null || corpus.CorpusDigest != request.Corpus.CorpusDigest ||
            published.CorpusId != corpusId || published.CorpusDigest != corpus.CorpusDigest ||
            published.OperationKind != OperationKind.FinalizeCorpus || published.OperationId != stored.OperationId) return null;
        var ledger = JsonSerializer.Deserialize<ImmutableArray<Contributor>>(corpus.ContributorsJson, _json);
        var retainedLedger = JsonSerializer.Deserialize<ImmutableArray<Contributor>>(stored.ContributorsJson, _json);
        var publishedLedger = JsonSerializer.Deserialize<ImmutableArray<Contributor>>(published.ContributorsJson, _json);
        if (ledger.IsDefault || retainedLedger.IsDefault || publishedLedger.IsDefault ||
            ledger.Any(x => x is null) || retainedLedger.Any(x => x is null) || publishedLedger.Any(x => x is null)) return null;
        var memberRepositories = await (from member in context.CorpusDocuments.AsNoTracking()
            join manifest in context.AcceptedFileManifests.AsNoTracking()
                on new { member.BatchId, member.ManifestId } equals new { manifest.BatchId, manifest.ManifestId }
            where member.BatchId == batch && member.CorpusId == corpusId
            select manifest.RepositoryId).Distinct().Take(OctoberKnowledgeStoredReadBudget.MaximumItems + 1).ToListAsync(ct);
        budget.Add(memberRepositories.Select(_ => 16L).ToArray());
        var original = advisoryRows.Where(x => memberRepositories.Contains(x.RepositoryId) &&
            x.Scope is (int)OctoberContextScope.System or (int)OctoberContextScope.Repository).Select(row =>
        {
            var repository = repositories.Single(x => x.RepositoryId == row.RepositoryId);
            var contributor = new Contributor((ContributorKind)(row.Scope + 1),
                row.Scope == (int)OctoberContextScope.Repository ? row.RepositoryId : null,
                row.Scope == (int)OctoberContextScope.System ? repository.SystemId : null,
                null, row.OriginRecordId, OctoberKnowledgePolicy.Hash(row.Value!));
            return new BusinessSynthesisAdvisoryContext(row.RepositoryId, (OctoberContextScope)row.Scope, contributor, row.Value!);
        }).ToImmutableArray();
        if (original.Any(x => !ledger.Contains(x.Contributor))) return null;
        var projected = OctoberBusinessContextOverridePolicy.Project(original, request.Selection.ContextOverride);
        var additional = projected.Where(x => !ledger.Contains(x.Contributor)).ToImmutableArray();
        var expected = ledger.Concat(additional.Select(x => x.Contributor)).ToHashSet();
        if (!expected.SetEquals(retainedLedger) || !expected.SetEquals(publishedLedger)) return null;
        return additional.Select(x => new KnowledgeAdvisoryBinding(x.RepositoryId, x.Contributor.Kind,
            x.Contributor.AdvisoryRecordId!.Value, x.Contributor.ContentDigest)).ToImmutableArray();
    }
}
