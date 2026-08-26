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

    internal static Guid ProviderContributionId(string contributionFingerprint) =>
        DeterministicGuid(contributionFingerprint);

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
