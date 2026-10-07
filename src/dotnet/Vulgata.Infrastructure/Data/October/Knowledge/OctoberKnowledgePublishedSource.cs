using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Loads only immutable, exact published membership. No latest or legacy fallback.</summary>
public sealed class OctoberKnowledgePublishedSource(IDbContextFactory<OctoberDbContext> factory) : IOctoberPublishedKnowledgeSource
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<OctoberDbContext> _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task<OctoberPublishedKnowledge?> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        await using var context = await _factory.CreateDbContextAsync(token);
        if (!await IsSelectedAsync(context, publication, token)) return null;
        Guid batch = publication.Knowledge.BatchId;
        Guid publicationId = publication.PublicationId;
        var budget = new OctoberKnowledgeStoredReadBudget();
        var versionsQuery = from member in context.PublicationMembers.AsNoTracking()
                            join version in context.DocumentVersions.AsNoTracking()
                                on new { member.BatchId, member.LogicalDocumentId, member.VersionId }
                                equals new { version.BatchId, version.LogicalDocumentId, version.VersionId }
                            join logical in context.LogicalDocuments.AsNoTracking()
                                on new { member.BatchId, member.LogicalDocumentId }
                                equals new { logical.BatchId, logical.LogicalDocumentId }
                            where member.BatchId == batch && member.PublicationId == publicationId
                            select new { Version = version, logical.Kind };
        if (context.Database.IsNpgsql())
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(v."ContentJson"::text)::bigint + octet_length(v."Title") AS "Value"
                FROM october."OctoberPublicationMembers" m
                JOIN october."OctoberDocumentVersions" v ON v."BatchId" = m."BatchId" AND v."VersionId" = m."VersionId" AND v."LogicalDocumentId" = m."LogicalDocumentId"
                WHERE m."BatchId" = {batch} AND m."PublicationId" = {publicationId} LIMIT 10001
                """).ToListAsync(token), bytes: true);
        else
            budget.Add(await versionsQuery.Select(x => (long)x.Version.ContentJson.Length + x.Version.Title.Length)
                .Take(OctoberKnowledgeStoredReadBudget.MaximumItems + 1).ToListAsync(token));
        var versions = await versionsQuery.ToListAsync(token);
        if (versions.Count == 0 || versions.Any(x => x.Kind != DocumentKind.CodeLogic || x.Version.SchemaVersion != KnowledgeContract.SchemaVersion))
            return null;

        var coverageQuery = context.PublicationCoverage.AsNoTracking().Where(x => x.BatchId == batch && x.PublicationId == publicationId);
        if (context.Database.IsNpgsql())
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length("CoverageJson"::text)::bigint + octet_length("ContributorsJson"::text) AS "Value"
                FROM october."OctoberPublicationCoverage"
                WHERE "BatchId" = {batch} AND "PublicationId" = {publicationId} LIMIT 10001
                """).ToListAsync(token), bytes: true);
        else
            budget.Add(await coverageQuery.Select(x => (long)x.CoverageJson.Length + x.ContributorsJson.Length)
                .Take(OctoberKnowledgeStoredReadBudget.MaximumItems + 1).ToListAsync(token));
        var coverage = await coverageQuery.ToListAsync(token);
        var uncertaintyQuery = from member in context.PublicationUncertainties.AsNoTracking()
                               join value in context.KnowledgeUncertainties.AsNoTracking()
                                   on new { member.BatchId, member.ManifestId, member.Key }
                                   equals new { value.BatchId, value.ManifestId, value.Key }
                               where member.BatchId == batch && member.PublicationId == publicationId
                               select value;
        if (context.Database.IsNpgsql())
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(u."ContentJson"::text)::bigint AS "Value"
                FROM october."OctoberPublicationUncertainties" p
                JOIN october."OctoberKnowledgeUncertainties" u ON u."BatchId" = p."BatchId" AND u."ManifestId" = p."ManifestId" AND u."Key" = p."Key"
                WHERE p."BatchId" = {batch} AND p."PublicationId" = {publicationId} LIMIT 10001
                """).ToListAsync(token), bytes: true);
        else
            budget.Add(await uncertaintyQuery.Select(x => (long)x.ContentJson.Length)
                .Take(OctoberKnowledgeStoredReadBudget.MaximumItems + 1).ToListAsync(token));
        var uncertainties = await uncertaintyQuery.ToListAsync(token);
        var manifests = versions.Select(x => x.Version.ManifestId).Concat(uncertainties.Select(x => x.ManifestId)).Distinct().ToArray();
        var contributorsQuery = context.KnowledgeContributors.AsNoTracking().Where(x => x.BatchId == batch && manifests.Contains(x.ManifestId));
        if (context.Database.IsNpgsql())
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(c."ContributorJson"::text)::bigint AS "Value"
                FROM october."KnowledgeContributors" c WHERE c."BatchId" = {batch} AND (
                    EXISTS (SELECT 1 FROM october."OctoberPublicationMembers" m
                        JOIN october."OctoberDocumentVersions" v ON v."BatchId" = m."BatchId" AND v."VersionId" = m."VersionId" AND v."LogicalDocumentId" = m."LogicalDocumentId"
                        WHERE m."BatchId" = c."BatchId" AND m."PublicationId" = {publicationId} AND v."ManifestId" = c."ManifestId")
                    OR EXISTS (SELECT 1 FROM october."OctoberPublicationUncertainties" u
                        WHERE u."BatchId" = c."BatchId" AND u."PublicationId" = {publicationId} AND u."ManifestId" = c."ManifestId")) LIMIT 100001
                """).ToListAsync(token), OctoberKnowledgeStoredReadBudget.MaximumContributors, bytes: true);
        else
            budget.Add(await contributorsQuery.Select(x => (long)x.ContributorJson.Length)
                .Take(OctoberKnowledgeStoredReadBudget.MaximumContributors + 1).ToListAsync(token), OctoberKnowledgeStoredReadBudget.MaximumContributors);
        var contributorRows = await contributorsQuery.OrderBy(x => x.Ordinal).ToListAsync(token);
        var contributors = contributorRows.GroupBy(x => x.ManifestId).ToDictionary(x => x.Key,
            x => x.Select(row => Required<Contributor>(row.ContributorJson)).ToImmutableArray());
        if (manifests.Any(x => !contributors.ContainsKey(x))) throw new JsonException("Missing accepted contributor provenance.");

        var documents = ImmutableArray.CreateBuilder<OctoberPublishedDocument>(versions.Count);
        foreach (var item in versions)
        {
            var version = item.Version;
            // PostgreSQL jsonb normalizes representation; verify the canonical semantic payload.
            string content = OctoberKnowledgePolicy.CanonicalizeJson(version.ContentJson);
            if (OctoberKnowledgePolicy.Hash(content) != version.ContentDigest)
                throw new JsonException("Accepted content digest mismatch.");
            using var canonical = JsonDocument.Parse(content);
            if (canonical.RootElement.GetProperty("schemaVersion").GetInt32() != KnowledgeContract.SchemaVersion ||
                canonical.RootElement.GetProperty("title").GetString() != version.Title)
                throw new JsonException("Accepted content schema mismatch.");
            var sections = canonical.RootElement.GetProperty("sections").Deserialize<ImmutableArray<DocumentSection>>(_json);
            if (sections.IsDefaultOrEmpty) throw new JsonException("Missing accepted sections.");
            documents.Add(new(new(batch, version.LogicalDocumentId, version.VersionId), item.Kind, version.Title,
                content, sections, contributors[version.ManifestId]));
        }
        var resolution = await LoadResolutionAsync(context, publication, budget, token);
        token.ThrowIfCancellationRequested();
        return new(publication, documents.ToImmutable(),
            coverage.Select(x => new OctoberPublishedCoverage(Required<FileCoverage>(x.CoverageJson), RequiredContributors(x.ContributorsJson))).ToImmutableArray(),
            resolution.Relationships, uncertainties.Select(x => new OctoberPublishedUncertainty(Required<Uncertainty>(x.ContentJson), contributors[x.ManifestId],
                versions.Where(version => version.Version.ManifestId == x.ManifestId)
                    .Select(version => new DocumentVersionReference(batch, version.Version.LogicalDocumentId, version.Version.VersionId)).ToImmutableArray()))
                .Concat(resolution.Uncertainties).ToImmutableArray());
    }

    private static async Task<(ImmutableArray<OctoberPublishedRelationship> Relationships, ImmutableArray<OctoberPublishedUncertainty> Uncertainties)>
        LoadResolutionAsync(OctoberDbContext context, PublicationBinding publication, OctoberKnowledgeStoredReadBudget budget, CancellationToken token)
    {
        Guid batch = publication.Knowledge.BatchId;
        Guid publicationId = publication.PublicationId;
        var relationships = from selected in context.PublicationRelationships.AsNoTracking()
                            join edge in context.RelationshipVersions.AsNoTracking()
                                on new { selected.BatchId, selected.CorpusId, selected.RelationshipVersionId }
                                equals new { edge.BatchId, edge.CorpusId, edge.RelationshipVersionId }
                            join item in context.ResolutionItems.AsNoTracking()
                                on new { edge.BatchId, edge.CorpusId, edge.ItemId } equals new { item.BatchId, item.CorpusId, item.ItemId }
                            where selected.BatchId == batch && selected.PublicationId == publicationId
                            select new { Edge = edge, item.ContributorsJson };
        var uncertainties = from selected in context.PublicationResolutionUncertainties.AsNoTracking()
                            join unknown in context.UncertaintyVersions.AsNoTracking()
                                on new { selected.BatchId, selected.CorpusId, selected.UncertaintyId }
                                equals new { unknown.BatchId, unknown.CorpusId, unknown.UncertaintyId }
                            join item in context.ResolutionItems.AsNoTracking()
                                on new { unknown.BatchId, unknown.CorpusId, unknown.ItemId } equals new { item.BatchId, item.CorpusId, item.ItemId }
                            where selected.BatchId == batch && selected.PublicationId == publicationId
                            select new { Unknown = unknown, item.ContributorsJson };
        if (context.Database.IsNpgsql())
        {
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(e."ContentJson"::text)::bigint + octet_length(i."ContributorsJson"::text) AS "Value"
                FROM october."OctoberPublicationRelationships" p
                JOIN october."OctoberRelationshipVersions" e ON e."BatchId"=p."BatchId" AND e."CorpusId"=p."CorpusId" AND e."RelationshipVersionId"=p."RelationshipVersionId"
                JOIN october."OctoberResolutionItems" i ON i."BatchId"=e."BatchId" AND i."CorpusId"=e."CorpusId" AND i."ItemId"=e."ItemId"
                WHERE p."BatchId"={batch} AND p."PublicationId"={publicationId} LIMIT 10001
                """).ToListAsync(token), bytes: true);
            budget.Add(await context.Database.SqlQuery<long>($"""
                SELECT octet_length(u."ContentJson"::text)::bigint + octet_length(i."ContributorsJson"::text) AS "Value"
                FROM october."OctoberPublicationResolutionUncertainties" p
                JOIN october."OctoberUncertaintyVersions" u ON u."BatchId"=p."BatchId" AND u."CorpusId"=p."CorpusId" AND u."UncertaintyId"=p."UncertaintyId"
                JOIN october."OctoberResolutionItems" i ON i."BatchId"=u."BatchId" AND i."CorpusId"=u."CorpusId" AND i."ItemId"=u."ItemId"
                WHERE p."BatchId"={batch} AND p."PublicationId"={publicationId} LIMIT 10001
                """).ToListAsync(token), bytes: true);
        }
        else
        {
            budget.Add(await relationships.Select(x => (long)x.Edge.ContentJson.Length + x.ContributorsJson.Length).Take(10001).ToListAsync(token));
            budget.Add(await uncertainties.Select(x => (long)x.Unknown.ContentJson.Length + x.ContributorsJson.Length).Take(10001).ToListAsync(token));
        }
        var edges = await relationships.ToListAsync(token);
        var unknowns = await uncertainties.ToListAsync(token);
        var itemIds = unknowns.Select(x => x.Unknown.ItemId).Distinct().ToArray();
        var dependencies = context.ResolutionDocumentDependencies.AsNoTracking().Where(x => x.BatchId == batch && itemIds.Contains(x.ItemId) && x.IsInquiryCaller);
        budget.Add(await dependencies.Select(x => 48L).Take(10001).ToListAsync(token), bytes: true);
        var owners = await dependencies.ToListAsync(token);
        if (itemIds.Any(itemId => owners.Count(owner => owner.ItemId == itemId) != 1))
            throw new JsonException("An accepted resolution uncertainty requires one exact inquiry caller.");
        return (edges.Select(x => new OctoberPublishedRelationship(Required<AcceptedRelationship>(x.Edge.ContentJson), RequiredContributors(x.ContributorsJson))).ToImmutableArray(),
            unknowns.Select(x => new OctoberPublishedUncertainty(Required<Uncertainty>(x.Unknown.ContentJson), RequiredContributors(x.ContributorsJson),
                owners.Where(owner => owner.ItemId == x.Unknown.ItemId).Select(owner => new DocumentVersionReference(batch, owner.LogicalDocumentId, owner.VersionId)).ToImmutableArray())).ToImmutableArray());
    }

    internal static async Task<bool> IsSelectedAsync(OctoberDbContext context, PublicationBinding publication, CancellationToken token)
    {
        if (publication?.Knowledge is not { } binding || binding.BatchId == Guid.Empty || publication.PublicationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(binding.TargetId) || string.IsNullOrWhiteSpace(binding.CanonicalDatabaseIdentity) ||
            string.IsNullOrWhiteSpace(binding.SelectionDigest)) return false;
        return await context.Batches.AsNoTracking().AnyAsync(x => x.BatchId == binding.BatchId && x.TargetId == binding.TargetId &&
            x.CanonicalDatabaseIdentity == binding.CanonicalDatabaseIdentity && x.SelectionDigest == binding.SelectionDigest, token) &&
            await context.KnowledgePublications.AsNoTracking().AnyAsync(x => x.BatchId == binding.BatchId && x.PublicationId == publication.PublicationId, token);
    }

    private static T Required<T>(string json) where T : class => JsonSerializer.Deserialize<T>(json, _json)
        ?? throw new JsonException("Missing accepted value.");
    private static ImmutableArray<Contributor> RequiredContributors(string json)
    {
        var value = JsonSerializer.Deserialize<ImmutableArray<Contributor>>(json, _json);
        if (value.IsDefaultOrEmpty) throw new JsonException("Missing accepted contributor provenance.");
        return value;
    }
}

/// <summary>Preflights SQL lengths before fetching payloads. PostgreSQL JSON uses octet_length; other string lengths use a conservative four-byte bound.</summary>
internal sealed class OctoberKnowledgeStoredReadBudget
{
    internal const int MaximumItems = 10_000;
    internal const int MaximumContributors = 100_000;
    private long _bytes;
    internal void Add(IReadOnlyCollection<long> sizes, int maximumRows = MaximumItems, bool bytes = false)
    {
        if (sizes.Count > maximumRows || sizes.Any(x => x < 0)) throw new OctoberKnowledgeReadBudgetException();
        foreach (long size in sizes) _bytes = checked(_bytes + (bytes ? size : checked(size * 4)));
        if (_bytes > 32L * 1024 * 1024) throw new OctoberKnowledgeReadBudgetException();
    }
}
