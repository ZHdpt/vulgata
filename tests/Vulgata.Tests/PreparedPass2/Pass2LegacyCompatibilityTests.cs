namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2LegacyCompatibilityTests
{
    [Fact]
    public void LegacyExactCrossRepositoryEdgeEvidence_RemainsReadableAndUnchanged()
    {
        Type legacy = typeof(Vulgata.Core.Entities.ExactCrossRepositoryEdgeEvidence);
        Assert.NotNull(legacy.GetProperty("EdgeId"));
        Assert.NotNull(legacy.GetProperty("ConsumerDocumentVersionId"));
        Assert.NotNull(legacy.GetProperty("ProviderDocumentVersionId"));
        Assert.NotNull(legacy.GetProperty("NormalizedEndpointIdentifier"));
        Assert.NotNull(legacy.GetProperty("Protocol"));
        Assert.NotNull(legacy.GetProperty("EvidenceFingerprint"));
        Assert.Null(legacy.GetProperty("ResolutionDecisionId"));
    }

    [Fact]
    public void DocumentEdge_RemainsReusableByDocumentVersionTuple()
    {
        Type edge = typeof(Vulgata.Core.Entities.DocumentEdge);
        Assert.NotNull(edge.GetProperty("SourceDocumentVersionId"));
        Assert.NotNull(edge.GetProperty("TargetDocumentVersionId"));
        Assert.NotNull(edge.GetProperty("EdgeType"));
    }
}
