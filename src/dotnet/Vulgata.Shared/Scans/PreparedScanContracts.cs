using System.Text.Json.Serialization;

namespace Vulgata.Shared.Scans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PreparedScanRequest
{
    public IReadOnlyList<PreparedScanRepositoryRevisionBinding> Repositories { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PreparedScanRepositoryRevisionBinding
{
    public Guid RepositoryId { get; set; }

    public string GitRef { get; set; } = string.Empty;

    public string ProfiledRevision { get; set; } = string.Empty;
}

public sealed record PreparedScanResponse(
    Guid CoordinationId,
    string PairSelectionFingerprint,
    string PairPackageFingerprint,
    IReadOnlyList<PreparedScanManifestResponse> Manifests,
    int SelectedCount);

public sealed record PreparedScanManifestResponse(
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    Guid ManifestId,
    string ManifestFingerprint,
    string CodeGraphProofIdentityHash,
    string JavaReadinessIdentityHash,
    string PreparedScanContextIdentityHash,
    IReadOnlyList<PreparedScanEntryResponse> Entries);

public sealed record PreparedScanEntryResponse(
    Guid FileCodeUnitId,
    string FileCodeUnitIdentityFingerprint,
    int ManifestOrdinal,
    int PairSelectionOrdinal);
