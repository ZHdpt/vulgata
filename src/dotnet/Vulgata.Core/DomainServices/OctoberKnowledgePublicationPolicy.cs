using System.Collections.Immutable;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

public enum CorpusFreezeEligibility
{
    Eligible, AdmissionOpen, Unsettled, Paused, HardStopped, Denied, AuthorityUnavailable
}

/// <summary>
/// Mandatory trusted Scan-host verification of the exact terminal input inventory and request digest.
/// Eligible requires closed admission, settled accepted/rolled-back operations and no unknown writes.
/// Producer-supplied coverage alone is not authority. Closed inventory is immutable; the shared commit
/// guard rechecks phase eligibility, hard stop, deadline and live authority at actual submission.
/// Freeze/publication use their operation ID as CommitAttempt.AttemptId, not a file attempt.
/// </summary>
public interface IOctoberCorpusFreezeAuthority
{
    Task<CorpusFreezeEligibility> VerifyAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default);
}

public static class OctoberKnowledgePublicationPolicy
{
    public static string ComputeFreezeDigest(CorpusFreezeRequest request) => OctoberKnowledgePolicy.Hash(
        OctoberKnowledgePolicy.SerializeCanonical(new
        {
            schemaVersion = KnowledgeContract.SchemaVersion,
            request.Operation,
            request.Binding,
            acceptedFiles = request.AcceptedFiles.OrderBy(x => x.ManifestId.ToString("D"), StringComparer.Ordinal).Select(x => new
            {
                x.ManifestId, x.AdmissionId, x.ProposalDigest,
                documents = OrderedDocuments(x.Documents),
                coverage = NormalizeCoverage(x.Coverage)
            }),
            terminalCoverage = OrderedCoverage(request.TerminalCoverage)
        }));

    public static string ComputePublicationDigest(PublicationRequest request) => OctoberKnowledgePolicy.Hash(
        OctoberKnowledgePolicy.SerializeCanonical(new
        {
            schemaVersion = KnowledgeContract.SchemaVersion,
            request.Operation,
            request.Corpus,
            documents = OrderedDocuments(request.Documents),
            relationships = request.Relationships.OrderBy(x => x.RelationshipVersionId.ToString("D"), StringComparer.Ordinal),
            uncertainties = request.Uncertainties.OrderBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal),
            flows = request.Flows.OrderBy(x => x.LogicalFlowId.ToString("D"), StringComparer.Ordinal),
            coverage = OrderedCoverage(request.Coverage)
        }));

    public static FileCoverage NormalizeCoverage(FileCoverage coverage) => coverage with
    {
        Origin = coverage.Origin with { RelativePath = OctoberKnowledgePolicy.NormalizePath(coverage.Origin.RelativePath) }
    };

    public static bool ValidCoverage(FileCoverage coverage)
    {
        var origin = coverage.Origin;
        string path = OctoberKnowledgePolicy.NormalizePath(origin.RelativePath);
        bool validOrigin = origin.RepositoryId != Guid.Empty && !string.IsNullOrWhiteSpace(origin.RootBindingIdentity) &&
            !string.IsNullOrWhiteSpace(origin.PreparedVersion) && origin.SourceDigest is { Length: 64 } &&
            origin.SourceDigest.All(char.IsAsciiHexDigit) && !string.IsNullOrWhiteSpace(path) &&
            !path.StartsWith('/') && !path.Contains(':') && !path.Any(char.IsControl) &&
            path.Split('/').All(part => part.Length > 0 && part is not "." and not ".." && part == part.Trim());
        return coverage.AdmissionId != Guid.Empty && validOrigin && Enum.IsDefined(coverage.Disposition) &&
            (coverage.Disposition == CoverageDisposition.Accepted || !string.IsNullOrWhiteSpace(coverage.Reason));
    }

    public static bool SameCoverage(FileCoverage left, FileCoverage right) => NormalizeCoverage(left) == NormalizeCoverage(right);

    public static ImmutableArray<DocumentVersionReference> OrderedDocuments(ImmutableArray<DocumentVersionReference> documents) =>
        documents.OrderBy(x => x.LogicalDocumentId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(x => x.DocumentVersionId.ToString("D"), StringComparer.Ordinal).ToImmutableArray();

    public static ImmutableArray<FileCoverage> OrderedCoverage(ImmutableArray<FileCoverage> coverage) =>
        coverage.Select(NormalizeCoverage).OrderBy(x => x.AdmissionId.ToString("D"), StringComparer.Ordinal).ToImmutableArray();
}
