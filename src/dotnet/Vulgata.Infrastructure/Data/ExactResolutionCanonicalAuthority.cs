using System.Security.Cryptography;
using System.Text;

using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal static class ExactResolutionCanonicalAuthority
{
    private const string _profileSchemaVersion = "1.0";
    private const string _resolutionKind = "ExactConfigured";

    internal static string EvidenceFingerprint(
        ExactResolutionDecisionBatch batch,
        ExactResolutionDecision decision,
        ExactResolutionDecisionEvidence evidence) =>
        EvidenceFingerprint(
            batch.ProfileId,
            batch.ProfileVersion,
            batch.ProfileFingerprint,
            decision.Identity,
            decision.Owner.RepositoryId,
            decision.Owner.ScanRunId,
            decision.Owner.DocumentVersionId,
            decision.Owner.SourceAnchorId,
            decision.Owner.ProfiledRevision,
            decision.Target!.RepositoryId,
            decision.Target.ScanRunId,
            decision.Target.DocumentVersionId,
            decision.Target.SourceAnchorId,
            decision.Target.ProfiledRevision,
            evidence.ConsumerRawIdentifier,
            evidence.ProviderRawIdentifier,
            evidence.NormalizedIdentifier,
            evidence.Protocol,
            _resolutionKind,
            evidence.Confidence,
            evidence.ConsumerEvidenceLocator,
            evidence.ProviderEvidenceLocator);

    internal static string EvidenceFingerprint(
        ExactCrossRepositoryEdgeEvidence evidence,
        string consumerProfiledRevision,
        string providerProfiledRevision) =>
        EvidenceFingerprint(
            evidence.ProfileId,
            evidence.ProfileVersion,
            evidence.ProfileFingerprint,
            evidence.OccurrenceId,
            evidence.ConsumerRepositoryId,
            evidence.ConsumerScanRunId,
            evidence.ConsumerDocumentVersionId,
            evidence.ConsumerSourceAnchorId,
            consumerProfiledRevision,
            evidence.ProviderRepositoryId,
            evidence.ProviderScanRunId,
            evidence.ProviderDocumentVersionId,
            evidence.ProviderSourceAnchorId,
            providerProfiledRevision,
            evidence.ConsumerRawIdentifier,
            evidence.ProviderRawIdentifier,
            evidence.NormalizedEndpointIdentifier,
            evidence.Protocol,
            evidence.ResolutionKind,
            evidence.Confidence,
            evidence.ConsumerEvidenceLocator,
            evidence.ProviderEvidenceLocator);

    internal static Guid DecisionId(string evidenceFingerprint) =>
        DeterministicGuid(evidenceFingerprint);

    internal static string ProviderContributionFingerprint(
        string decisionEvidenceFingerprint,
        Guid providerDocumentVersionId,
        Guid sourceAnchorId) =>
        HashIdentity(
            "provider-contribution",
            decisionEvidenceFingerprint,
            providerDocumentVersionId.ToString("D"),
            sourceAnchorId.ToString("D"));

    internal static string ProviderObservationFingerprint(
        string decisionEvidenceFingerprint,
        ProviderEndpointCandidate observation)
    {
        if (observation.CommunicationClassificationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(observation.ClassificationFingerprint) ||
            string.IsNullOrWhiteSpace(observation.CatalogRuleIdentity) ||
            string.IsNullOrWhiteSpace(observation.NormalizationRuleIdentity))
        {
            throw new ArgumentException(
                "A provider observation requires complete current classification authority.",
                nameof(observation));
        }

        return HashIdentity(
            "provider-contribution",
            decisionEvidenceFingerprint,
            observation.Owner.RepositoryId.ToString("D"),
            observation.Owner.ScanRunId.ToString("D"),
            observation.Owner.ProfiledRevision,
            observation.Owner.DocumentVersionId.ToString("D"),
            observation.Owner.SourceAnchorId.ToString("D"),
            observation.CommunicationClassificationId.ToString("D"),
            observation.DeclarationEvidenceLocator,
            observation.ClassificationFingerprint,
            observation.CatalogRuleIdentity,
            observation.NormalizationRuleIdentity,
            observation.RawIdentifier,
            observation.NormalizedIdentifier);
    }

    internal static string PersistedProviderContributionFingerprint(
        string decisionEvidenceFingerprint,
        ExactResolutionProviderEvidenceContribution contribution) =>
        HashIdentity(
            "provider-contribution",
            decisionEvidenceFingerprint,
            contribution.RepositoryId.ToString("D"),
            contribution.ScanRunId.ToString("D"),
            contribution.ProfiledRevision,
            contribution.ProviderDocumentVersionId.ToString("D"),
            contribution.SourceAnchorId.ToString("D"),
            contribution.CommunicationClassificationId.ToString("D"),
            contribution.EvidenceLocator,
            contribution.ClassificationFingerprint,
            contribution.CatalogRuleIdentity,
            contribution.NormalizationRuleIdentity,
            contribution.ProviderRawIdentifier,
            contribution.NormalizedEndpointIdentifier);

    internal static Guid ProviderContributionId(string contributionFingerprint) =>
        DeterministicGuid(contributionFingerprint);

    internal static string ProviderEvidenceSetFingerprint(
        IEnumerable<string> contributionFingerprints)
    {
        ArgumentNullException.ThrowIfNull(contributionFingerprints);
        string[] ordered = [.. contributionFingerprints];
        if (ordered.Length == 0 || ordered.Any(value =>
                value.Length != 64 || value.Any(character =>
                    character is not (>= '0' and <= '9') and
                        not (>= 'a' and <= 'f'))))
        {
            throw new ArgumentException(
                "A provider evidence set requires canonical contribution fingerprints.",
                nameof(contributionFingerprints));
        }

        return Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', ordered))));
    }

    internal static bool MatchesPersistedExactAuthority(
        CrossRepositoryResolutionDecision decision,
        DocumentEdge edge,
        IReadOnlyCollection<ExactResolutionProviderEvidenceContribution> contributions)
    {
        if (!IsCanonicalFingerprint(decision.EvidenceFingerprint) ||
            !IsCanonicalFingerprint(decision.ConsumerEvidenceFingerprint) ||
            !IsCanonicalFingerprint(decision.ProviderEvidenceSetFingerprint) ||
            !StringComparer.Ordinal.Equals(
                decision.ResolutionSchemaIdentity,
                "exact-cross-repository-resolution/2.0") ||
            string.IsNullOrWhiteSpace(decision.NormalizationRuleIdentity) ||
            !StringComparer.Ordinal.Equals(decision.OutcomeKind, "ExactMatch") ||
            !StringComparer.Ordinal.Equals(decision.ReasonCode, "unique_provider") ||
            !decision.ProviderIdentity.HasValue ||
            decision.ProviderIdentity.Value == Guid.Empty ||
            !decision.ProviderDocumentVersionId.HasValue ||
            !decision.ProviderRepositoryId.HasValue ||
            !decision.ProviderScanRunId.HasValue ||
            string.IsNullOrWhiteSpace(decision.ProviderProfiledRevision) ||
            decision.UncertaintyId is not null || decision.ResolvedUncertaintyId is not null ||
            !StringComparer.Ordinal.Equals(
                decision.DecisionFingerprint, decision.EvidenceFingerprint) ||
            decision.Id != DecisionId(decision.EvidenceFingerprint) ||
            decision.ConsumerDocumentVersionId != edge.SourceDocumentVersionId ||
            decision.ConsumerRepositoryId != edge.SourceRepositoryId ||
            decision.ConsumerScanRunId != edge.SourceScanRunId ||
            decision.ProviderDocumentVersionId != edge.TargetDocumentVersionId ||
            decision.ProviderRepositoryId != edge.TargetRepositoryId ||
            decision.ProviderScanRunId != edge.TargetScanRunId ||
            decision.DocumentEdgeId != edge.Id ||
            contributions.Count == 0)
        {
            return false;
        }

        Guid expectedProviderIdentity = ProviderIdentity(
            decision.SystemId,
            decision.ProviderRepositoryId.Value,
            decision.ProviderScanRunId.Value,
            decision.ProviderProfiledRevision,
            decision.ProviderDocumentVersionId.Value);
        if (decision.ProviderIdentity.Value != expectedProviderIdentity)
        {
            return false;
        }

        var contributionFingerprints = new List<string>(contributions.Count);
        foreach (ExactResolutionProviderEvidenceContribution contribution in contributions
            .OrderBy(value => value.ProviderOrdinal)
            .ThenBy(value => value.EvidenceOrdinal))
        {
            string fingerprint = PersistedProviderContributionFingerprint(
                decision.EvidenceFingerprint, contribution);
            if (contribution.ResolutionDecisionId != decision.Id ||
                contribution.ProviderIdentity != decision.ProviderIdentity ||
                contribution.ProviderDocumentVersionId != decision.ProviderDocumentVersionId ||
                contribution.RepositoryId != decision.ProviderRepositoryId ||
                contribution.ScanRunId != decision.ProviderScanRunId ||
                !StringComparer.Ordinal.Equals(
                    contribution.ProfiledRevision,
                    decision.ProviderProfiledRevision) ||
                contribution.CommunicationClassificationId == Guid.Empty ||
                !IsCanonicalFingerprint(contribution.ClassificationFingerprint) ||
                string.IsNullOrWhiteSpace(contribution.CatalogRuleIdentity) ||
                !StringComparer.Ordinal.Equals(
                    contribution.NormalizationRuleIdentity,
                    decision.NormalizationRuleIdentity) ||
                !StringComparer.Ordinal.Equals(contribution.EvidenceFingerprint, fingerprint) ||
                contribution.Id != ProviderContributionId(fingerprint))
            {
                return false;
            }

            contributionFingerprints.Add(fingerprint);
        }

        return StringComparer.Ordinal.Equals(
            decision.ProviderEvidenceSetFingerprint,
            ProviderEvidenceSetFingerprint(contributionFingerprints));
    }

    private static bool IsCanonicalFingerprint(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static Guid ProviderIdentity(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        Guid documentVersionId) =>
        DeterministicGuid(HashIdentity(
            "provider-identity",
            systemId.ToString("D"),
            repositoryId.ToString("D"),
            scanRunId.ToString("D"),
            profiledRevision,
            documentVersionId.ToString("D")));

    private static string EvidenceFingerprint(
        string profileId,
        string profileVersion,
        string profileFingerprint,
        string occurrenceId,
        Guid consumerRepositoryId,
        Guid consumerScanRunId,
        Guid consumerDocumentVersionId,
        Guid consumerSourceAnchorId,
        string consumerProfiledRevision,
        Guid providerRepositoryId,
        Guid providerScanRunId,
        Guid providerDocumentVersionId,
        Guid providerSourceAnchorId,
        string providerProfiledRevision,
        string consumerRawIdentifier,
        string providerRawIdentifier,
        string normalizedIdentifier,
        ExactResolutionProtocol protocol,
        string resolutionKind,
        decimal confidence,
        string consumerEvidenceLocator,
        string providerEvidenceLocator)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write("vulgata.exact-cross-repository-edge-evidence/1");
            writer.Write(_profileSchemaVersion);
            writer.Write(profileId);
            writer.Write(profileVersion);
            writer.Write(profileFingerprint);
            writer.Write(occurrenceId);
            WriteOwner(writer, consumerRepositoryId, consumerScanRunId,
                consumerDocumentVersionId, consumerSourceAnchorId, consumerProfiledRevision);
            WriteOwner(writer, providerRepositoryId, providerScanRunId,
                providerDocumentVersionId, providerSourceAnchorId, providerProfiledRevision);
            writer.Write(consumerRawIdentifier);
            writer.Write(providerRawIdentifier);
            writer.Write(normalizedIdentifier);
            writer.Write(protocol.ToString());
            writer.Write(resolutionKind);
            writer.Write(confidence);
            writer.Write(consumerEvidenceLocator);
            writer.Write(providerEvidenceLocator);
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteOwner(
        BinaryWriter writer,
        Guid repositoryId,
        Guid scanRunId,
        Guid documentVersionId,
        Guid sourceAnchorId,
        string profiledRevision)
    {
        writer.Write(repositoryId.ToByteArray());
        writer.Write(scanRunId.ToByteArray());
        writer.Write(documentVersionId.ToByteArray());
        writer.Write(sourceAnchorId.ToByteArray());
        writer.Write(profiledRevision);
    }

    private static string HashIdentity(string discriminator, params string[] values)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write($"vulgata.exact-resolution-{discriminator}/2");
            foreach (string value in values)
            {
                writer.Write(value);
            }
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static Guid DeterministicGuid(string fingerprint)
    {
        byte[] bytes = Convert.FromHexString(fingerprint)[..16];
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}
