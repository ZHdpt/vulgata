using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// QR-US5-018/019 contract REDs. These checks name the durable authority
/// fields required to reconstruct complete provider observations and
/// non-exact resolution/accounting outcomes; they do not invent a second
/// runtime operation or inspect private implementation details.
/// </summary>
public sealed class Pass2Us5QualityCorrectionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Pass2StoreAndCommitter_DoNotUseLegacyContractFallbacks()
    {
        DirectoryInfo? repository = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (repository is not null &&
            !File.Exists(Path.Combine(repository.FullName, "Vulgata.slnx")))
        {
            repository = repository.Parent;
        }

        string root = repository?.FullName ?? Directory.GetCurrentDirectory();
        string[] sources =
        [
            Path.Combine(root, "src", "dotnet", "Vulgata.Infrastructure", "Data", "PreparedPass2Store.cs"),
            Path.Combine(root, "src", "dotnet", "Vulgata.Infrastructure", "Data", "Pass2BusinessLogicDocumentCommitter.cs"),
        ];

        foreach (string sourcePath in sources)
        {
            Assert.True(File.Exists(sourcePath), $"Expected Pass 2 production seam: {sourcePath}");
            string source = File.ReadAllText(sourcePath);
            Assert.DoesNotContain("pass2-business-logic-worker.v1", source, StringComparison.Ordinal);
            Assert.DoesNotContain("business-logic/flow-v1", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_legacy", source, StringComparison.Ordinal);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ProviderContributionPersistence_DoesNotUseConstantProviderOrdinal()
    {
        DirectoryInfo? repository = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (repository is not null &&
            !File.Exists(Path.Combine(repository.FullName, "Vulgata.slnx")))
        {
            repository = repository.Parent;
        }

        string root = repository?.FullName ?? Directory.GetCurrentDirectory();
        string sourcePath = Path.Combine(
            root,
            "src",
            "dotnet",
            "Vulgata.Infrastructure",
            "Data",
            "ExactCrossRepositoryResolutionStore.cs");
        Assert.True(File.Exists(sourcePath), $"Expected Pass 2 resolution store: {sourcePath}");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("ProviderOrdinal = 1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderOrdinal).CurrentValue = 1", source, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ProviderContributionPersistence_DoesNotUseLegacyAuthorityDefaults()
    {
        DirectoryInfo? repository = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (repository is not null &&
            !File.Exists(Path.Combine(repository.FullName, "Vulgata.slnx")))
        {
            repository = repository.Parent;
        }

        string root = repository?.FullName ?? Directory.GetCurrentDirectory();
        string sourcePath = Path.Combine(
            root,
            "src",
            "dotnet",
            "Vulgata.Infrastructure",
            "Data",
            "ExactCrossRepositoryResolutionStore.cs");
        Assert.True(File.Exists(sourcePath), $"Expected Pass 2 resolution store: {sourcePath}");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain(
            "?? \"prepared-communication-catalog/2.0\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "?? \"exact-endpoint-normalization/2.0\"",
            source,
            StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ProviderContribution_PersistsCompleteObservationAuthority()
    {
        string[] required =
        [
            nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId),
            nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId),
            nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint),
            nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal),
            nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal),
            "EvidenceLocator",
            "ClassificationFingerprint",
            "CatalogRuleIdentity",
            "NormalizationRuleIdentity",
        ];

        PropertyInfo[] properties = typeof(ExactResolutionProviderEvidenceContribution)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.All(required, name => Assert.Contains(
            properties,
            property => StringComparer.Ordinal.Equals(property.Name, name)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void NonExactResolution_PersistsCanonicalOutcomeOwnershipAndReplayAccounting()
    {
        string[] decisionFields =
        [
            nameof(CrossRepositoryResolutionDecision.ResolutionSchemaIdentity),
            nameof(CrossRepositoryResolutionDecision.OutcomeKind),
            nameof(CrossRepositoryResolutionDecision.ReasonCode),
            nameof(CrossRepositoryResolutionDecision.UncertaintyId),
            nameof(CrossRepositoryResolutionDecision.ResolvedUncertaintyId),
            nameof(CrossRepositoryResolutionDecision.DecisionFingerprint),
            nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint),
        ];
        PropertyInfo[] decisionProperties = typeof(CrossRepositoryResolutionDecision)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.All(decisionFields, name => Assert.Contains(
            decisionProperties,
            property => StringComparer.Ordinal.Equals(property.Name, name)));

        string[] executionFields =
        [
            "CandidateCount",
            "ResolutionFingerprint",
            "PlanningFingerprint",
            "ExecutionFingerprint",
        ];
        PropertyInfo[] executionProperties = typeof(Pass2Execution)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.All(executionFields, name => Assert.Contains(
            executionProperties,
            property => StringComparer.Ordinal.Equals(property.Name, name)));
    }
}
