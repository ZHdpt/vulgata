using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;

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
        // Continued Business generation may consume edited advisory snapshots. Their
        // accepted digests belong to this exact publication, not to the original setup.
        var ledgerQuery = context.KnowledgePublications.AsNoTracking()
            .Where(x => x.BatchId == batch && x.PublicationId == publication.PublicationId);
        if (context.Database.IsNpgsql())
            budget.Add(await context.Database.SqlQuery<long>($"""
                                                              SELECT octet_length("ContributorsJson"::text)::bigint AS "Value"
                                                              FROM october."OctoberKnowledgePublications" WHERE "BatchId"={batch} AND "PublicationId"={publication.PublicationId}
                                                              """).ToListAsync(token), 1, bytes: true);
        else
            budget.Add(await ledgerQuery.Select(x => (long)x.ContributorsJson.Length).ToListAsync(token), 1);
        var ledger = JsonSerializer.Deserialize<ImmutableArray<Contributor>>(await ledgerQuery.Select(x => x.ContributorsJson).SingleAsync(token),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (ledger.IsDefault) throw new JsonException("Missing publication provenance.");
        var publishedAdvisory = from contributor in ledger
            where contributor.AdvisoryRecordId.HasValue &&
                  contributor.Kind is ContributorKind.GlobalAdvisory or ContributorKind.SystemAdvisory or ContributorKind.RepositoryAdvisory
            from repository in repositories
            where contributor.Kind == ContributorKind.GlobalAdvisory ||
                  contributor.Kind == ContributorKind.SystemAdvisory && repository.SystemId == contributor.SystemId ||
                  contributor.Kind == ContributorKind.RepositoryAdvisory && repository.RepositoryId == contributor.RepositoryId
            select new KnowledgeAdvisoryBinding(repository.RepositoryId, contributor.Kind, contributor.AdvisoryRecordId!.Value, contributor.ContentDigest);
        advisory = advisory.Concat(publishedAdvisory).Distinct().ToImmutableArray();
        token.ThrowIfCancellationRequested();
        return new(publication, repositories.ToImmutableArray(), advisory);
    }
}
