using System.Collections.Immutable;
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
        token.ThrowIfCancellationRequested();
        return new(publication, repositories.ToImmutableArray(), advisory);
    }
}
