using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

/// <summary>
/// Pure reconstruction of the canonical direct-plus-reconciled REFERENCES
/// Edge union used to validate a durable reconciliation replay.
/// </summary>
internal static class PreparedPass1ReconciliationProjector
{
    public static IReadOnlyList<PreparedPass1ReconciledEdge> ProjectExpectedUnion(
        IReadOnlyList<Pass1ReferenceObservation> observations,
        IReadOnlyList<DocumentVersion> currentVersions,
        IReadOnlyList<SourceAnchor> currentAnchors)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(currentVersions);
        ArgumentNullException.ThrowIfNull(currentAnchors);

        HashSet<(Guid Source, Guid Target)> directKeys = observations
            .Where(value => value.DirectTargetDocumentVersionId is not null)
            .Select(value => (
                value.SourceDocumentVersionId,
                value.DirectTargetDocumentVersionId!.Value))
            .ToHashSet();
        var acceptedKeys = new HashSet<(Guid Source, Guid Target)>();
        var acceptedEdges = new List<PreparedPass1ReconciledEdge>();
        foreach (Pass1ReferenceObservation observation in observations)
        {
            if (!observation.CanResolve)
            {
                continue;
            }

            DocumentVersion[] targets = currentVersions.Where(target =>
                    target.RepositoryId == observation.SourceRepositoryId &&
                    target.ScanRunId == observation.SourceScanRunId &&
                    target.ProfiledRevision == observation.SourceProfiledRevision &&
                    (observation.DirectTargetDocumentVersionId is null ||
                        target.Id == observation.DirectTargetDocumentVersionId) &&
                    (observation.TargetCanonicalPath is null ||
                        target.RepositoryRelativePath == observation.TargetCanonicalPath) &&
                    observation.TargetSourceHash is not null &&
                    currentAnchors.Any(anchor =>
                        anchor.DocumentVersionId == target.Id &&
                        anchor.SourceHash == observation.TargetSourceHash))
                .OrderBy(value => value.Id)
                .ToArray();
            if (targets.Length != 1)
            {
                continue;
            }

            var key = (observation.SourceDocumentVersionId, targets[0].Id);
            if (key.Item1 == key.Item2 || !acceptedKeys.Add(key))
            {
                continue;
            }

            if (!directKeys.Contains(key) && WouldCreateCycle(key, directKeys, acceptedEdges))
            {
                acceptedKeys.Remove(key);
                continue;
            }

            PreparedPass1CanonicalArtifact identity =
                PreparedPass1CanonicalCodec.CreateReferencesEdge(key.Item1, key.Item2);
            acceptedEdges.Add(new(identity.Id!.Value, key.Item1, key.Item2));
        }

        return acceptedEdges;
    }

    public static bool MatchesDurableUnion(
        IReadOnlyList<PreparedPass1ReconciledEdge> expectedEdges,
        IReadOnlyList<DocumentEdge> actualEdges,
        IReadOnlyList<Pass1ReferenceObservation> observations,
        IReadOnlyList<DocumentVersion> sourceVersions,
        IReadOnlyList<DocumentVersion> currentVersions,
        DateTimeOffset reconciledAt)
    {
        ArgumentNullException.ThrowIfNull(expectedEdges);
        ArgumentNullException.ThrowIfNull(actualEdges);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(sourceVersions);
        ArgumentNullException.ThrowIfNull(currentVersions);

        if (reconciledAt.Offset != TimeSpan.Zero ||
            expectedEdges.Count != actualEdges.Count ||
            expectedEdges.Select(value => value.Id).Distinct().Count() != expectedEdges.Count ||
            expectedEdges.Select(value => (value.SourceDocumentVersionId, value.TargetDocumentVersionId))
                .Distinct().Count() != expectedEdges.Count ||
            actualEdges.Select(value => value.Id).Distinct().Count() != actualEdges.Count ||
            actualEdges.Select(value => (value.SourceDocumentVersionId, value.TargetDocumentVersionId))
                .Distinct().Count() != actualEdges.Count)
        {
            return false;
        }

        Dictionary<Guid, DocumentVersion> sources = sourceVersions
            .GroupBy(value => value.Id)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        Dictionary<Guid, DocumentVersion> targets = currentVersions
            .GroupBy(value => value.Id)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        if (sources.Count != sourceVersions.Count || targets.Count != currentVersions.Count)
        {
            return false;
        }

        for (int index = 0; index < expectedEdges.Count; index++)
        {
            PreparedPass1ReconciledEdge expected = expectedEdges[index];
            if (!sources.TryGetValue(expected.SourceDocumentVersionId, out DocumentVersion? source) ||
                !targets.TryGetValue(expected.TargetDocumentVersionId, out DocumentVersion? target))
            {
                return false;
            }

            PreparedPass1CanonicalArtifact identity =
                PreparedPass1CanonicalCodec.CreateReferencesEdge(source.Id, target.Id);
            bool isDirect = observations.Any(value =>
                value.SourceDocumentVersionId == source.Id &&
                value.DirectTargetDocumentVersionId == target.Id);
            DocumentEdge? actual = actualEdges.SingleOrDefault(value => value.Id == expected.Id);
            if (identity.Id != expected.Id || actual is null ||
                actual.SourceDocumentVersionId != source.Id ||
                actual.SourceRepositoryId != source.RepositoryId ||
                actual.SourceScanRunId != source.ScanRunId ||
                actual.TargetDocumentVersionId != target.Id ||
                actual.TargetRepositoryId != target.RepositoryId ||
                actual.TargetScanRunId != target.ScanRunId ||
                actual.EdgeType != DocumentEdgeType.References ||
                (actual.CreatedAt != reconciledAt && (!isDirect || actual.CreatedAt != source.CreatedAt)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool WouldCreateCycle(
        (Guid Source, Guid Target) candidate,
        IReadOnlySet<(Guid Source, Guid Target)> directEdges,
        IReadOnlyList<PreparedPass1ReconciledEdge> acceptedEdges)
    {
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(candidate.Target);
        while (pending.TryPop(out Guid current))
        {
            if (current == candidate.Source)
            {
                return true;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            foreach (Guid next in directEdges
                         .Where(value => value.Source == current)
                         .Select(value => value.Target)
                         .Concat(acceptedEdges
                             .Where(value => value.SourceDocumentVersionId == current)
                             .Select(value => value.TargetDocumentVersionId)))
            {
                pending.Push(next);
            }
        }

        return false;
    }
}
