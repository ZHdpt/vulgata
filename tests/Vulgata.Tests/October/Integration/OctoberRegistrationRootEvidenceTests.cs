using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberRegistrationRootEvidenceTests
{
    private static PreparedOrigin Origin() => new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        "owned-root", "AccountPolicy.java", new string('A', 64), "prepared-exact");

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(4, 6)]
    [InlineData(1, 14)]
    [InlineData(3, 7)]
    public void MethodOrBroaderAcceptedAnchor_PreservesExactOfferedIdentity(int start, int end)
    {
        var origin = Origin(); Guid offered = Guid.NewGuid();
        bool eligible = OctoberRegistrationRootEvidence.CoversRegistration([new(origin, start, end)], origin, 14, 4, 5);
        Assert.True(eligible);
        Assert.Equal(offered, OctoberRegistrationRootEvidence.RequireUnique(eligible ? [offered] : []));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(8, 13)]
    [InlineData(5, 6)]
    [InlineData(1, 4)]
    public void MissingDeclarationOrReturn_DoesNotSelectUnrelatedOrPartialEvidence(int start, int end)
    {
        var origin = Origin();
        Assert.False(OctoberRegistrationRootEvidence.CoversRegistration([new(origin, start, end)], origin, 14, 4, 5));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BroadAndMethodCandidatesBothQualify_NoPreferenceOrFirstFallback()
    {
        var origin = Origin();
        (Guid Id, ImmutableArray<SourceAnchor> Anchors)[] offered =
            [(Guid.NewGuid(), [new(origin, 1, 14)]), (Guid.NewGuid(), [new(origin, 4, 6)])];
        var eligible = offered.Where(x => OctoberRegistrationRootEvidence.CoversRegistration(x.Anchors, origin, 14, 4, 5))
            .Select(x => x.Id).ToArray();
        Assert.Equal(2, eligible.Length);
        Assert.Throws<InvalidOperationException>(() => OctoberRegistrationRootEvidence.RequireUnique(eligible));
        Assert.Throws<InvalidOperationException>(() => OctoberRegistrationRootEvidence.RequireUnique([]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(0, 6)]
    [InlineData(5, 4)]
    [InlineData(1, 15)]
    public void AnyInvalidAnchorRangeRejectsEvenAlongsideValidEvidence(int start, int end)
    {
        var origin = Origin();
        Assert.Throws<InvalidOperationException>(() => OctoberRegistrationRootEvidence.CoversRegistration(
            [new(origin, 4, 6), new(origin, start, end)], origin, 14, 4, 5));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChangedOriginFieldsOrMissingAnchorsReject()
    {
        var origin = Origin();
        PreparedOrigin[] changed = [origin with { RepositoryId = Guid.NewGuid() }, origin with { RootBindingIdentity = "foreign" },
            origin with { RelativePath = "Other.java" }, origin with { SourceDigest = new string('B', 64) },
            origin with { PreparedVersion = "other-version" }];
        foreach (var foreign in changed)
            Assert.Throws<InvalidOperationException>(() => OctoberRegistrationRootEvidence.CoversRegistration(
                [new(origin, 4, 6), new(foreign, 1, 14)], origin, 14, 4, 5));
        Assert.Throws<InvalidOperationException>(() => OctoberRegistrationRootEvidence.CoversRegistration([], origin, 14, 4, 5));
        Assert.Throws<InvalidOperationException>(() => OctoberRegistrationRootEvidence.CoversRegistration(default, origin, 14, 4, 5));
    }
}
