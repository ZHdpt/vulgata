using System.Collections.Immutable;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;

namespace Vulgata.Web.Explore;

public static class KnowledgeGraphProjector
{
    public const int MaximumNodes = 40;
    public const int MaximumEdges = 80;
    public static ExploreGraph Project(ImmutableArray<SearchPage> seedPages,
        ImmutableArray<RelatedPage> relatedPages, bool acquisitionIncomplete)
    {
        if (seedPages.IsDefault || relatedPages.IsDefault) { throw new ArgumentException("缺少图谱数据。"); }
        var nodes = new Dictionary<DocumentHandle, DocumentSummary>();
        foreach (var page in seedPages)
        {
            foreach (var node in page.Documents)
            {
                if (nodes.TryGetValue(node.Handle, out var existing) && JsonSerializer.Serialize(existing) != JsonSerializer.Serialize(node))
                {
                    throw new ArgumentException("同一文档版本的图谱数据不一致。");
                }

                nodes.TryAdd(node.Handle, node);
            }
        }

        var visibleNodes = nodes.Values.Take(MaximumNodes).ToImmutableArray();
        var visibleHandles = visibleNodes.Select(node => node.Handle).ToHashSet();
        var edges = new Dictionary<Guid, RelatedDocument>();
        var uncertainties = new Dictionary<string, Uncertainty>(StringComparer.Ordinal);
        foreach (var page in relatedPages)
        {
            foreach (var edge in page.Relationships)
            {
                if (edge.RelationshipVersionId is not { } id || id == Guid.Empty)
                {
                    throw new ArgumentException("关系缺少已接受的版本标识。");
                }

                if (edges.TryGetValue(id, out var existing) && existing != edge)
                {
                    throw new ArgumentException("同一关系版本的图谱数据不一致。");
                }

                edges.TryAdd(id, edge);
            }

            foreach (var uncertainty in page.PermittedUncertainties)
            {
                // A producer-local Key is not a graph-wide identity. Collapse only identical full values.
                var value = OctoberKnowledgePolicy.CanonicalizeJson(JsonSerializer.Serialize(uncertainty));
                uncertainties.TryAdd(value, uncertainty);
            }
        }

        var visibleEdges = edges.Values.Where(edge => visibleHandles.Contains(edge.From) && visibleHandles.Contains(edge.To)).ToArray();
        var coverages = seedPages.Select(page => page.Coverage).Concat(relatedPages.Select(page => page.Coverage)).ToArray();
        var knownCoverage = coverages.OfType<CoverageProjection>().ToArray();
        var coverage = knownCoverage.Length == 0 ? null : new CoverageProjection(knownCoverage.Any(item => item.IsPartial),
            knownCoverage.SelectMany(item => item.PermittedGaps).Distinct(StringComparer.Ordinal).ToImmutableArray());
        return new(visibleNodes, visibleEdges.Take(MaximumEdges).ToImmutableArray(), uncertainties.Values.ToImmutableArray(),
            acquisitionIncomplete || nodes.Count > MaximumNodes || visibleEdges.Length > MaximumEdges || edges.Count > visibleEdges.Length ||
                seedPages.Any(page => page.Next is not null) || relatedPages.Any(page => page.Next is not null),
            coverage, coverages.Any(item => item is null));
    }
}
