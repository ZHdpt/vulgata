using Vulgata.Tests.TestInfrastructure;
using System.Reflection;

namespace Vulgata.Tests.PreparedPass2;

public sealed class ExactResolutionContractTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void V2ResolutionDecision_ExposesDecisionOwnedOutcomeAndProviderEvidence()
    {
        Type type = FindCoreType("CrossRepositoryResolutionDecision");

        Assert.NotNull(type.GetProperty("Protocol"));
        Assert.NotNull(type.GetProperty("NormalizedEndpointIdentifier"));
        Assert.NotNull(type.GetProperty("ConsumerEntryStableKey"));
        Assert.NotNull(type.GetProperty("ProviderDocumentVersionId"));
        Assert.NotNull(type.GetProperty("DocumentEdgeId"));
        Assert.NotNull(type.GetProperty("UncertaintyId"));
        Assert.NotNull(type.GetProperty("Outcome"));
        Assert.NotNull(type.GetProperty("EvidenceFingerprint"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ProviderEvidenceContribution_IsOwnedByDecision_NotReusableEdge()
    {
        Type type = FindCoreType("ExactResolutionProviderEvidenceContribution");

        Assert.NotNull(type.GetProperty("ResolutionDecisionId"));
        Assert.NotNull(type.GetProperty("ProviderDocumentVersionId"));
        Assert.NotNull(type.GetProperty("SourceAnchorId"));
        Assert.NotNull(type.GetProperty("EvidenceFingerprint"));
        Assert.Null(type.GetProperty("DocumentEdgeId"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResolutionContracts_ExposeDeterministicProtocolAndProviderAggregationSeams()
    {
        Assembly core = typeof(Vulgata.Core.Entities.DocumentEdge).Assembly;
        Assert.NotNull(core.GetType(
            "Vulgata.Core.DomainServices.ExactResolutionDecisionContract",
            throwOnError: false));
        Assert.NotNull(core.GetType(
            "Vulgata.Core.DomainServices.ExactResolutionProviderEvidenceContract",
            throwOnError: false));
    }

    private static Type FindCoreType(string name) =>
        typeof(Vulgata.Core.Entities.DocumentEdge).Assembly.GetType(
            $"Vulgata.Core.Entities.{name}",
            throwOnError: false) ??
        throw new Xunit.Sdk.XunitException(
            $"Expected Story 12.4 public Core seam is absent: {name}.");
}
