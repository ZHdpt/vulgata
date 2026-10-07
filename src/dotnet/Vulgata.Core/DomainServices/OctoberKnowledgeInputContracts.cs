using System.Text;
using System.Text.Json;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

public enum SourceMetadataOperation
{
    List = 0, ReadTextRange = 1, SearchText = 2, JavaDefinition = 3, JavaReferences = 4, CodeGraphExplore = 5,
}

/// <summary>Provenance of admitted metadata, never verification of file bytes or permission to read source.</summary>
public sealed record SourceMetadataOrigin(Guid RepositoryId, string RootBindingIdentity, string PreparedVersion,
    SourceMetadataOperation Operation, string NormalizedScopePath, string ResultDigest);

public static class OctoberKnowledgeInputLimits
{
    public const int MaximumContentOriginsIncludingPrimary = 128;
    public const int MaximumMetadataObservations = 128;
    public const int MaximumMetadataProjectionUtf8Bytes = 64 * 1024;
    public const int MaximumSafeRequestUtf8Bytes = 64 * 1024;
}

/// <summary>Structural input validation is distinct from the trusted completion ledger and exact source verification.</summary>
public static class OctoberKnowledgeInputPolicy
{
    private static readonly UTF8Encoding _utf8 = new(false, true);

    public static string ComputeMetadataResultDigest(SourceMetadataOrigin origin, string safeRequestJson, string admittedProjectionJson)
    {
        if (!MetadataIdentityValid(origin)) throw new ArgumentException("Invalid prepared metadata identity.", nameof(origin));
        string request = OctoberKnowledgePolicy.CanonicalizeJson(safeRequestJson);
        string projection = OctoberKnowledgePolicy.CanonicalizeJson(admittedProjectionJson);
        if (_utf8.GetByteCount(request) > OctoberKnowledgeInputLimits.MaximumSafeRequestUtf8Bytes ||
            _utf8.GetByteCount(projection) > OctoberKnowledgeInputLimits.MaximumMetadataProjectionUtf8Bytes)
            throw new ArgumentException("Metadata request or admitted projection exceeds its bound.");
        using var requestDocument = JsonDocument.Parse(request);
        using var projectionDocument = JsonDocument.Parse(projection);
        if (requestDocument.RootElement.ValueKind != JsonValueKind.Object || projectionDocument.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Metadata request and admitted projection must be JSON objects.");
        return OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/source-metadata/1", origin.RepositoryId, origin.RootBindingIdentity, origin.PreparedVersion,
            origin.Operation, scopePath = origin.NormalizedScopePath,
            request = requestDocument.RootElement, projection = projectionDocument.RootElement,
        }));
    }

    public static bool IsValidMetadataContributor(Contributor contributor) => contributor is
        { Kind: ContributorKind.SourceMetadata, SourceOrigin: null, AdvisoryRecordId: null, MetadataOrigin: { } origin } &&
        MetadataIdentityValid(origin) && Digest(origin.ResultDigest) && contributor.RepositoryId == origin.RepositoryId &&
        contributor.RootBindingIdentity == origin.RootBindingIdentity && contributor.ContentDigest == origin.ResultDigest;

    public static bool IsValidSourceContributor(Contributor contributor) => contributor is
        { Kind: ContributorKind.Source, MetadataOrigin: null, AdvisoryRecordId: null, RepositoryId: { } repository } &&
        repository != Guid.Empty && Text(contributor.RootBindingIdentity) && Digest(contributor.ContentDigest) &&
        (contributor.SourceOrigin is null || IsValidContentOrigin(contributor.SourceOrigin) &&
            contributor.SourceOrigin.RepositoryId == repository && contributor.SourceOrigin.RootBindingIdentity == contributor.RootBindingIdentity &&
            contributor.SourceOrigin.SourceDigest == contributor.ContentDigest);

    public static bool IsValidContentOrigin(PreparedOrigin origin) => origin is not null && origin.RepositoryId != Guid.Empty &&
        Text(origin.RootBindingIdentity) && Text(origin.PreparedVersion) && Digest(origin.SourceDigest) && origin.RelativePath is not null &&
        PathValid(OctoberKnowledgePolicy.NormalizePath(origin.RelativePath), false);

    private static bool MetadataIdentityValid(SourceMetadataOrigin origin) => origin is not null && origin.RepositoryId != Guid.Empty &&
        Text(origin.RootBindingIdentity) && Text(origin.PreparedVersion) && Enum.IsDefined(origin.Operation) && PathValid(origin.NormalizedScopePath, true);
    private static bool PathValid(string? path, bool allowRoot) => path is not null && ((allowRoot && path == ".") ||
        Text(path) && !path.Contains('\\') && !path.Contains(':') && !path.StartsWith('/') && !path.Any(char.IsControl) &&
        path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not ".." && segment == segment.Trim()));
    private static bool Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { _ = _utf8.GetByteCount(value); return true; }
        catch (EncoderFallbackException) { return false; }
    }
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
