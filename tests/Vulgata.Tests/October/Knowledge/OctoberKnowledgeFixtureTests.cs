using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberKnowledgeFixtureTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_MultiUnitAndZeroUnit_PreservesExplicitControlledProvenance()
    {
        var fixture = new OctoberKnowledgeFixture();
        Assert.Equal("ControlledFixture", OctoberKnowledgeFixture.Provenance);
        Assert.StartsWith("ControlledFixture/", fixture.Binding.CanonicalDatabaseIdentity);
        Assert.Equal(2, fixture.MultiUnit.Units.Length);
        Assert.Equal(fixture.MultiUnit.Units[0].SemanticSelector, fixture.MultiUnit.Units[1].ParentSelector);
        Assert.Empty(fixture.ZeroUnit.Units);
        Assert.Equal(CoverageDisposition.CompletedZeroUnit, fixture.ZeroUnit.Coverage.Disposition);
        Assert.NotEmpty(fixture.ZeroUnit.Coverage.Reason!);
        Assert.Equal(fixture.ZeroUnit.Origin, fixture.ZeroUnit.Coverage.Origin);
        Assert.Equal(fixture.MultiUnit.CanonicalProposalDigest, new OctoberKnowledgeFixture().MultiUnit.CanonicalProposalDigest);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_GraphAndClaims_PreservesDirectionUnknownsAndDistinctRoots()
    {
        var fixture = new OctoberKnowledgeFixture();
        Assert.Equal(fixture.ClA, fixture.Relationship.From);
        Assert.Equal(fixture.ClB, fixture.Relationship.To);
        Assert.NotEmpty(fixture.Relationship.SupportingEvidence);
        Assert.NotEmpty(fixture.Unknown.Limitations);
        Assert.NotEqual(fixture.Flows[0].LogicalFlowId, fixture.Flows[1].LogicalFlowId);
        Assert.All(fixture.Flows, flow => Assert.Contains(fixture.ClB, flow.DocumentDependencies));
        Assert.Contains(fixture.Flows[0].Content.Sections[0].Claims, claim => claim.ClaimGroupId == fixture.SourceClaim.ClaimGroupId);
        Assert.Contains(fixture.Flows[0].Content.Sections[0].Claims, claim => claim.ClaimGroupId == fixture.AdvisoryClaim.ClaimGroupId);
        Assert.Equal(ClaimQualification.SourceSupported, fixture.SourceClaim.Qualification);
        Assert.Equal(ClaimQualification.Advisory, fixture.AdvisoryClaim.Qualification);
        Assert.Contains("AdvisoryNotSourceProof", fixture.AdvisoryClaim.Limitations);
        Assert.Null(fixture.AdvisoryClaim.Evidence[0].Source);
        foreach (var document in fixture.Documents)
        {
            foreach (var source in document.Claims.SelectMany(claim => claim.Evidence).Where(evidence => evidence.Source is not null))
                Assert.Contains(document.Contributors, contributor => contributor.RepositoryId == source.Source!.Origin.RepositoryId && contributor.ContentDigest == source.Source.Origin.SourceDigest);
        }
        var hidden = fixture.Documents.Single(d => d.Reference == fixture.BlHidden);
        Assert.Contains(fixture.SourceC, hidden.Contributors);
        Assert.DoesNotContain(hidden.Claims.SelectMany(c => c.Evidence), e => e.Document == fixture.ClC);
    }
}


