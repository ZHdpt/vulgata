using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal static class Pass2RelationshipAuthority
{
    internal static bool ValidateSupportingEdges(
        FlowGroup group,
        Guid providerRepositoryId,
        IReadOnlyList<FlowTraceStep> steps,
        IReadOnlyCollection<DocumentEdge> edges)
    {
        if (steps.Count < 5 || steps[3].EdgeId is not Guid implementationEdgeId ||
            !EdgeMatches(edges, implementationEdgeId, steps[3], steps[4], providerRepositoryId))
        {
            return false;
        }

        for (int index = 5; index < steps.Count; index += 2)
        {
            if (index + 1 >= steps.Count || steps[index].EdgeId is not Guid optionalEdgeId ||
                !EdgeMatches(edges, optionalEdgeId, steps[index], steps[index + 1],
                    providerRepositoryId))
            {
                return false;
            }
        }

        return edges.All(value => value.SourceRepositoryId == providerRepositoryId &&
            value.TargetRepositoryId == providerRepositoryId &&
            value.SourceRepositoryId != group.RepositoryId);
    }

    private static bool EdgeMatches(
        IReadOnlyCollection<DocumentEdge> edges,
        Guid edgeId,
        FlowTraceStep relationship,
        FlowTraceStep target,
        Guid repositoryId)
    {
        DocumentEdge[] matches = edges.Where(value => value.Id == edgeId).ToArray();
        return matches.Length == 1 && matches[0] is DocumentEdge edge &&
            edge.EdgeType == DocumentEdgeType.References &&
            edge.SourceDocumentVersionId == relationship.DocumentVersionId &&
            edge.SourceRepositoryId == relationship.DocumentRepositoryId &&
            edge.SourceScanRunId == relationship.DocumentScanRunId &&
            edge.TargetDocumentVersionId == target.DocumentVersionId &&
            edge.TargetRepositoryId == target.DocumentRepositoryId &&
            edge.TargetScanRunId == target.DocumentScanRunId &&
            edge.SourceRepositoryId == repositoryId &&
            edge.TargetRepositoryId == repositoryId &&
            edge.SourceDocumentVersionId != edge.TargetDocumentVersionId;
    }
}
