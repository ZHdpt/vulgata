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

/// <summary>Owner helper seam; its exact canonical projection is defined in supporting-inputs-contract.md.</summary>
public static class OctoberKnowledgeInputPolicy
{
    /// <summary>
    /// Hash identity, safe request parameters and the exact admitted filtered metadata projection.
    /// ResultDigest itself is excluded. This does not authenticate a host ledger or create source evidence.
    /// </summary>
    public static string ComputeMetadataResultDigest(SourceMetadataOrigin origin, string safeRequestJson,
        string admittedProjectionJson) => throw new NotImplementedException();
}
