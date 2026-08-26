using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.CrossRepository;

internal static class ExactResolutionProviderObservationCanonicalizer
{
    internal static ProviderEndpointCandidate[] Canonicalize(
        IEnumerable<ProviderEndpointCandidate> observations) =>
        observations
            .OrderBy(value => value.Owner.RepositoryId)
            .ThenBy(value => value.Owner.ScanRunId)
            .ThenBy(value => value.Owner.ProfiledRevision, StringComparer.Ordinal)
            .ThenBy(value => value.Owner.DocumentVersionId)
            .ThenBy(value => value.Owner.SourceAnchorId)
            .ThenBy(value => value.CommunicationClassificationId)
            .ThenBy(value => value.ClassificationFingerprint, StringComparer.Ordinal)
            .ThenBy(value => value.CatalogRuleIdentity, StringComparer.Ordinal)
            .ThenBy(value => value.NormalizationRuleIdentity, StringComparer.Ordinal)
            .ThenBy(value => value.DeclarationEvidenceLocator, StringComparer.Ordinal)
            .ThenBy(value => value.RawIdentifier, StringComparer.Ordinal)
            .ThenBy(value => value.NormalizedIdentifier, StringComparer.Ordinal)
            .ThenBy(value => value.OccurrenceId, StringComparer.Ordinal)
            .DistinctBy(ObservationKey)
            .ToArray();

    private static string ObservationKey(ProviderEndpointCandidate value) =>
        string.Join('\n',
            value.Owner.RepositoryId.ToString("D"),
            value.Owner.ScanRunId.ToString("D"),
            value.Owner.ProfiledRevision,
            value.Owner.DocumentVersionId.ToString("D"),
            value.Owner.SourceAnchorId.ToString("D"),
            value.CommunicationClassificationId.ToString("D"),
            value.ClassificationFingerprint ?? string.Empty,
            value.CatalogRuleIdentity ?? string.Empty,
            value.NormalizationRuleIdentity ?? string.Empty,
            value.DeclarationEvidenceLocator,
            value.RawIdentifier,
            value.NormalizedIdentifier,
            value.OccurrenceId);
}
