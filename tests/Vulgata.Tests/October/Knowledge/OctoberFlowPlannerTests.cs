using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberFlowPlannerTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_TwoExplicitRootsShareHelper_PreservesDistinctRootIdentity()
    {
        var fixture = new FlowPlannerCase(3, [(0, 2), (1, 2)], [0, 1]);
        var result = OctoberFlowPlanner.Plan(fixture.Input);
        Assert.Equal(FlowPlanningStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Groups.Length);
        var first = Group(result, fixture.Version(0));
        var second = Group(result, fixture.Version(1));
        Assert.NotEqual(first.Flow.LogicalFlowId, second.Flow.LogicalFlowId);
        Assert.Equal(fixture.Input.Units[0].UnitId, first.Flow.RootUnitId);
        Assert.Equal(fixture.Input.Corpus.Documents[0].RepositoryId, first.Flow.HomeRepositoryId);
        AssertMembers(first, fixture.Version(0), fixture.Version(2));
        AssertMembers(second, fixture.Version(1), fixture.Version(2));
        Assert.All(result.RootDispositions, value => Assert.Equal(FlowRootDispositionKind.Planned, value.Kind));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_LocalOnlyRoot_RequiresNeitherCrossRepositoryEdgeNorAnotherRoot()
    {
        var fixture = new FlowPlannerCase(1, [], [0]);
        var result = OctoberFlowPlanner.Plan(fixture.Input);
        Assert.Equal(FlowPlanningStatus.Succeeded, result.Status);
        var group = Assert.Single(result.Groups);
        AssertMembers(group, fixture.Version(0));
        Assert.Empty(group.Relationships);
        Assert.Empty(group.Frontiers);
        Assert.Equal(fixture.Input.Coverage.ToArray(), result.Coverage.ToArray());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_DirectedCycleAndInputPermutation_VisitsOnceAndKeepsCanonicalOrdering()
    {
        var fixture = new FlowPlannerCase(4, [(0, 1), (1, 2), (2, 0), (3, 0)], [0]);
        var first = OctoberFlowPlanner.Plan(fixture.Input);
        var permuted = fixture.Input with
        {
            Corpus = fixture.Input.Corpus with { Documents = fixture.Input.Corpus.Documents.Reverse().ToImmutableArray() },
            Units = fixture.Input.Units.Reverse().ToImmutableArray(),
            Resolutions = fixture.Input.Resolutions.Reverse().ToImmutableArray(),
            Coverage = fixture.Input.Coverage.Reverse().ToImmutableArray()
        };
        var second = OctoberFlowPlanner.Plan(permuted);
        var group = Assert.Single(first.Groups);
        AssertMembers(group, fixture.Version(0), fixture.Version(1), fixture.Version(2));
        Assert.Equal(3, group.Relationships.Length);
        Assert.DoesNotContain(group.Relationships, edge => edge.From == fixture.Version(3));
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(first), OctoberKnowledgePolicy.SerializeCanonical(second));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_DownstreamEvidenceChanges_RootIdentityStaysStableButGroupVersionChanges()
    {
        var first = new FlowPlannerCase(3, [(0, 1)], [0]);
        var second = new FlowPlannerCase(3, [(0, 1), (1, 2)], [0]);
        var left = Assert.Single(OctoberFlowPlanner.Plan(first.Input).Groups);
        var right = Assert.Single(OctoberFlowPlanner.Plan(second.Input).Groups);
        Assert.Equal(left.Flow.LogicalFlowId, right.Flow.LogicalFlowId);
        Assert.NotEqual(left.GroupVersionId, right.GroupVersionId);
        AssertMembers(left, first.Version(0), first.Version(1));
        AssertMembers(right, second.Version(0), second.Version(1), second.Version(2));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unit")]
    [InlineData("version")]
    [InlineData("foreign-batch")]
    [InlineData("selector")]
    public void Plan_RootDoesNotPinExactAcceptedUnit_ProducesNoFlow(string mismatch)
    {
        var fixture = new FlowPlannerCase(1, [], [0]);
        var root = fixture.Input.Roots[0];
        root = mismatch switch
        {
            "unit" => root with { UnitId = Guid.NewGuid() },
            "version" => root with { Document = root.Document with { DocumentVersionId = Guid.NewGuid() } },
            "foreign-batch" => root with { Document = root.Document with { BatchId = Guid.NewGuid() } },
            _ => root with { Selector = "guessed-folder-purpose" }
        };
        var result = OctoberFlowPlanner.Plan(fixture.Input with { Roots = [root] });
        Assert.Empty(result.Groups);
        Assert.Equal(FlowRootDispositionKind.InvalidRoot, Assert.Single(result.RootDispositions).Kind);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-anchor")]
    [InlineData("other-origin")]
    [InlineData("unaccepted-range")]
    public void Plan_UnitHasNoMatchingPrimaryAcceptedSourceAnchor_ProducesNoFlow(string mismatch)
    {
        var fixture = new FlowPlannerCase(1, [], [0]);
        var unit = fixture.Input.Units[0];
        unit = mismatch switch
        {
            "missing-anchor" => unit with { Anchors = [] },
            "other-origin" => unit with { PrimaryOrigin = unit.PrimaryOrigin with { RepositoryId = Guid.NewGuid() } },
            _ => unit with { Anchors = [new(unit.PrimaryOrigin, 9000, 9010)] }
        };
        var result = OctoberFlowPlanner.Plan(fixture.Input with { Units = [unit] });
        Assert.Empty(result.Groups);
        Assert.True(result.Status == FlowPlanningStatus.InvalidInput || result.RootDispositions.Any(value => value.Kind == FlowRootDispositionKind.InvalidRoot));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(FlowRootKind.ExportedOperation)]
    [InlineData(FlowRootKind.Handler)]
    [InlineData(FlowRootKind.Schedule)]
    [InlineData(FlowRootKind.DeclarativeWorkflow)]
    public void Plan_UnqualifiedRootKind_NamesAndConnectivityDoNotGrantRootAuthority(FlowRootKind kind)
    {
        var fixture = new FlowPlannerCase(2, [(0, 1)], [0]);
        var root = fixture.Input.Roots[0] with { Kind = kind };
        var result = OctoberFlowPlanner.Plan(fixture.Input with { Roots = [root] });
        Assert.Empty(result.Groups);
        Assert.Equal(FlowRootDispositionKind.UnsupportedKind, Assert.Single(result.RootDispositions).Kind);
        Assert.Empty(OctoberFlowPlanner.Plan(fixture.Input with { Roots = [] }).Groups);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("stale-target")]
    [InlineData("foreign-source")]
    [InlineData("wrong-kind")]
    [InlineData("unknown-inquiry")]
    [InlineData("duplicate-item")]
    public void Plan_InvalidAcceptedRelationshipProjection_FailsClosed(string mismatch)
    {
        var fixture = new FlowPlannerCase(2, [(0, 1)], [0]);
        var item = fixture.Input.Resolutions[0];
        var edge = item.Outcome.Relationship!;
        item = mismatch switch
        {
            "stale-target" => item with { Outcome = item.Outcome with { Relationship = edge with { To = edge.To with { DocumentVersionId = Guid.NewGuid() } } } },
            "foreign-source" => item with { Outcome = item.Outcome with { Relationship = edge with { From = edge.From with { BatchId = Guid.NewGuid() } } } },
            "wrong-kind" => item with { Outcome = item.Outcome with { Relationship = edge with { Kind = RelationshipKind.GeneratedFrom } } },
            "unknown-inquiry" => item with { Outcome = item.Outcome with { Fingerprint = new string('f', 64) } },
            _ => item
        };
        var result = OctoberFlowPlanner.Plan(fixture.Input with { Resolutions = mismatch == "duplicate-item" ? [item, item] : [item] });
        Assert.Equal(FlowPlanningStatus.InvalidInput, result.Status);
        Assert.Empty(result.Groups);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_SelectedInquiryUndisposed_BlocksEvenAnOtherwiseLocalRoot()
    {
        var fixture = new FlowPlannerCase(3, [(1, 2)], [0]);
        var result = OctoberFlowPlanner.Plan(fixture.Input with { Resolutions = [] });
        Assert.Equal(FlowPlanningStatus.IncompleteResolution, result.Status);
        Assert.Empty(result.Groups);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_OmittedExplicitPlanDisposition_CannotSilentlyDropUnexaminedInput()
    {
        var fixture = new FlowPlannerCase(1, [], [0], unsupportedObservation: true);
        Assert.NotEmpty(fixture.Input.PlanDispositions);
        var complete = OctoberFlowPlanner.Plan(fixture.Input);
        Assert.Equal(FlowPlanningStatus.Succeeded, complete.Status);
        Assert.Equal(fixture.Input.PlanDispositions.ToArray(), complete.PlanDispositions.ToArray());
        var missing = OctoberFlowPlanner.Plan(fixture.Input with { PlanDispositions = [] });
        Assert.Equal(FlowPlanningStatus.IncompleteResolution, missing.Status);
        Assert.Empty(missing.Groups);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(ResolutionOutcomeKind.Unresolved, FlowFrontierKind.Unresolved)]
    [InlineData(ResolutionOutcomeKind.Failed, FlowFrontierKind.Failed)]
    [InlineData(ResolutionOutcomeKind.NotEvaluated, FlowFrontierKind.NotEvaluated)]
    public void Plan_ExplicitNonSupportedDisposition_RetainsUncertaintyFrontierWithoutInventedEdge(ResolutionOutcomeKind kind, FlowFrontierKind frontierKind)
    {
        var fixture = new FlowPlannerCase(2, [(0, 1)], [0]);
        var item = fixture.Input.Resolutions[0];
        item = item with { Outcome = item.Outcome with
        {
            Kind = kind, Relationship = null,
            Uncertainty = item.Outcome.Uncertainty! with { Explanation = "Controlled " + kind + " disposition; no relationship was accepted." }
        } };
        var group = Assert.Single(OctoberFlowPlanner.Plan(fixture.Input with { Resolutions = [item] }).Groups);
        AssertMembers(group, fixture.Version(0));
        Assert.Empty(group.Relationships);
        var frontier = Assert.Single(group.Frontiers);
        Assert.Equal(frontierKind, frontier.Kind);
        Assert.Equal(fixture.Version(0), frontier.From);
        Assert.Null(frontier.To);
        Assert.Null(frontier.RelationshipVersionId);
        Assert.Equal(item.Outcome.Uncertainty!.Key, frontier.UncertaintyKey);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(item.Outcome.Uncertainty),
            OctoberKnowledgePolicy.SerializeCanonical(Assert.Single(group.Uncertainties).Content));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_AllSuppliedInputsAreDependenciesEvenOutsideMembershipAndCitations()
    {
        var fixture = new FlowPlannerCase(4, [(0, 1), (2, 3)], [0]);
        string original = OctoberKnowledgePolicy.SerializeCanonical(fixture.Input);
        var group = Assert.Single(OctoberFlowPlanner.Plan(fixture.Input).Groups);
        AssertMembers(group, fixture.Version(0), fixture.Version(1));
        Assert.True(fixture.Input.Corpus.Documents.Select(value => value.Version).ToHashSet().SetEquals(group.Dependencies.Documents));
        Assert.True(fixture.Input.Resolutions.Select(value => value.Outcome.Relationship!.RelationshipVersionId).ToHashSet()
            .SetEquals(group.Dependencies.Relationships.Select(value => value.RelationshipVersionId)));
        Assert.True(fixture.Input.Resolutions.Select(value => value.UncertaintyVersionId!.Value).ToHashSet()
            .SetEquals(group.Dependencies.Uncertainties.Select(value => value.UncertaintyVersionId)));
        Assert.True(fixture.Input.Corpus.Documents.SelectMany(value => value.Contributors).ToHashSet().SetEquals(group.Dependencies.Contributors));
        Assert.Contains(fixture.UncitedAdvisory, group.Dependencies.Contributors);
        Assert.All(group.Dependencies.Uncertainties, value => Assert.Contains(FlowPlannerCase.Limitation, value.Content.Limitations));
        Assert.Equal(original, OctoberKnowledgePolicy.SerializeCanonical(fixture.Input));
        Assert.Contains(fixture.Input.Corpus.Documents[3].Sections[0].Claims, claim => claim.Qualification == ClaimQualification.Advisory);
        Assert.Contains(fixture.Input.Corpus.Documents[3].Sections[0].Claims, claim => claim.Qualification == ClaimQualification.Hypothesis);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_SupportedOutcomeWithZeroUnitCoverageContributors_RetainsWholeCorpusLedger()
    {
        var fixture = new FlowPlannerCase(2, [(0, 1)], [0]);
        var origin = new PreparedOrigin(ResolutionCase.Id(88000), "controlled-zero-unit-root", "src/Empty.cs", new string('d', 64), "controlled-v1");
        var coverageSource = new Contributor(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest);
        var coverageAdvisory = new Contributor(ContributorKind.RepositoryAdvisory, origin.RepositoryId, null, null, ResolutionCase.Id(88001), new string('e', 64));
        var ledger = fixture.Input.Contributors.Add(coverageSource).Add(coverageAdvisory);
        var input = fixture.Input with
        {
            Coverage = fixture.Input.Coverage.Add(new(ResolutionCase.Id(88002), origin, CoverageDisposition.CompletedZeroUnit, null)),
            Contributors = ledger,
            // The store replaces the document-only policy union with its complete frozen-corpus ledger.
            Resolutions = fixture.Input.Resolutions.Select(item => item with { Outcome = item.Outcome with { Contributors = ledger } }).ToImmutableArray()
        };

        var result = OctoberFlowPlanner.Plan(input);

        Assert.Equal(FlowPlanningStatus.Succeeded, result.Status);
        var group = Assert.Single(result.Groups);
        AssertMembers(group, fixture.Version(0), fixture.Version(1));
        Assert.True(ledger.ToHashSet().SetEquals(group.Dependencies.Contributors));
        Assert.Contains(coverageSource, group.Dependencies.Contributors);
        Assert.Contains(coverageAdvisory, group.Dependencies.Contributors);
        Assert.Contains(result.Coverage, value => value.Origin == origin && value.Disposition == CoverageDisposition.CompletedZeroUnit);
        var documentOnlyOutcome = input with { Resolutions = fixture.Input.Resolutions };
        Assert.Equal(FlowPlanningStatus.InvalidInput, OctoberFlowPlanner.Plan(documentOnlyOutcome).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Plan_LocalOnlyRootWithUnprocessedCoverage_RetainsItsLineageAndRejectsMissingContributors()
    {
        var fixture = new FlowPlannerCase(1, [], [0]);
        var origin = new PreparedOrigin(ResolutionCase.Id(88100), "controlled-unprocessed-root", "src/Pending.cs", new string('f', 64), "controlled-v1");
        var coverageSource = new Contributor(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest);
        var input = fixture.Input with
        {
            Coverage = fixture.Input.Coverage.Add(new(ResolutionCase.Id(88101), origin, CoverageDisposition.Unprocessed, "Controlled graceful-stop gap.")),
            Contributors = fixture.Input.Contributors.Add(coverageSource)
        };

        var result = OctoberFlowPlanner.Plan(input);

        Assert.Equal(FlowPlanningStatus.Succeeded, result.Status);
        var group = Assert.Single(result.Groups);
        AssertMembers(group, fixture.Version(0));
        Assert.Empty(group.Relationships);
        Assert.Contains(coverageSource, group.Dependencies.Contributors);
        Assert.True(input.Contributors.ToHashSet().SetEquals(group.Dependencies.Contributors));
        Assert.Contains(result.Coverage, value => value.Origin == origin && value.Reason == "Controlled graceful-stop gap.");
        foreach (var incomplete in new[]
        {
            input with { Contributors = fixture.Input.Contributors },
            input with { Contributors = [coverageSource] },
            input with { Contributors = default }
        })
        {
            var rejected = OctoberFlowPlanner.Plan(incomplete);
            Assert.Equal(FlowPlanningStatus.InvalidInput, rejected.Status);
            Assert.Empty(rejected.Groups);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("nodes", FlowFrontierKind.NodeLimit)]
    [InlineData("edges", FlowFrontierKind.EdgeLimit)]
    public void Plan_ReducedReachableCeiling_ExactBoundaryAndOverflowRemainExplicit(string bound, FlowFrontierKind expected)
    {
        var fixture = new FlowPlannerCase(3, [(0, 1), (1, 2)], [0]);
        var exact = OctoberFlowPlanner.Plan(fixture.Input, new(3, 2, 16));
        Assert.Equal(3, Assert.Single(exact.Groups).Members.Length);
        Assert.Empty(exact.Groups[0].Frontiers);
        var limits = bound == "nodes" ? new FlowPlanningLimits(2, 2, 16) : new(3, 1, 16);
        var group = Assert.Single(OctoberFlowPlanner.Plan(fixture.Input, limits).Groups);
        AssertMembers(group, fixture.Version(0), fixture.Version(1));
        Assert.Single(group.Relationships);
        var frontier = Assert.Single(group.Frontiers);
        Assert.Equal(expected, frontier.Kind);
        Assert.Equal(fixture.Version(1), frontier.From);
        Assert.Equal(fixture.Version(2), frontier.To);
        Assert.NotNull(frontier.RelationshipVersionId);
        Assert.Equal(limits, group.Limits);
        Assert.Equal(3, group.Dependencies.Documents.Length);
        Assert.Equal(2, group.Dependencies.Relationships.Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(16)]
    [InlineData(17)]
    public void Plan_DefaultDepthBoundary_RootIsDepthZeroAndExcludedSuccessorIsVisible(int hops)
    {
        var fixture = new FlowPlannerCase(hops + 1, Enumerable.Range(0, hops).Select(index => (index, index + 1)).ToArray(), [0]);
        Assert.True(OctoberResolutionPolicy.Plan(fixture.Input.Corpus).Inquiries.Length <= 64);
        var group = Assert.Single(OctoberFlowPlanner.Plan(fixture.Input).Groups);
        Assert.Equal(17, group.Members.Length);
        Assert.Equal(16, group.Relationships.Length);
        if (hops == 16) Assert.Empty(group.Frontiers);
        else
        {
            var frontier = Assert.Single(group.Frontiers);
            Assert.Equal(FlowFrontierKind.DepthLimit, frontier.Kind);
            Assert.Equal(fixture.Version(16), frontier.From);
            Assert.Equal(fixture.Version(17), frontier.To);
            Assert.Equal(18, group.Dependencies.Documents.Length);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(257, 512, 16)]
    [InlineData(256, 513, 16)]
    [InlineData(256, 512, 17)]
    [InlineData(0, 512, 16)]
    [InlineData(256, -1, 16)]
    [InlineData(256, 512, -1)]
    public void Plan_LimitsCannotEnlargeHardCeilingsOrUseInvalidCounts(int nodes, int edges, int depth)
    {
        var fixture = new FlowPlannerCase(1, [], [0]);
        var result = OctoberFlowPlanner.Plan(fixture.Input, new(nodes, edges, depth));
        Assert.Equal(FlowPlanningStatus.InvalidInput, result.Status);
        Assert.Empty(result.Groups);
    }

    private static FlowGroupVersion Group(FlowPlanningResult result, DocumentVersionReference root) => result.Groups.Single(value => value.RootVersion == root);
    private static void AssertMembers(FlowGroupVersion group, params DocumentVersionReference[] expected)
    {
        Assert.Equal(expected.Length, group.Members.Length);
        Assert.True(expected.ToHashSet().SetEquals(group.Members));
    }
}

/// <summary>Controlled owner-projection-shaped data, not a claim of persisted or real-source acceptance. B1 policy derives every inquiry and supported outcome.</summary>
internal sealed class FlowPlannerCase
{
    public const string Limitation = "Static source evidence does not establish deployed availability.";
    public FlowPlanningInput Input { get; }
    public Contributor UncitedAdvisory { get; } = new(ContributorKind.GlobalAdvisory, null, null, null, Id(99000), new string('a', 64));
    private static Contributor CitedAdvisory => new(ContributorKind.GlobalAdvisory, null, null, null, Id(99001), new string('b', 64));
    public DocumentVersionReference Version(int index) => Input.Corpus.Documents[index].Version;

    public FlowPlannerCase(int count, (int From, int To)[] edges, int[] roots, bool unsupportedObservation = false)
    {
        var documents = ImmutableArray.CreateBuilder<ResolutionDocument>();
        var units = ImmutableArray.CreateBuilder<FlowPlanningUnit>();
        var coverage = ImmutableArray.CreateBuilder<FileCoverage>();
        for (int index = 0; index < count; index++)
        {
            var version = new DocumentVersionReference(Id(1), Id(1000 + index), Id(2000 + index));
            var origin = new PreparedOrigin(Id(4000 + index), "controlled-root-" + index, "src/HandlerWorkflow.cs", new string('c', 64), "controlled-v1");
            EvidenceReference Evidence(int line) => new(null, null, new(origin, line, line + 2), null, origin.SourceDigest);
            var claims = ImmutableArray.CreateBuilder<QualifiedClaimGroup>();
            var observations = ImmutableArray.CreateBuilder<CommunicationObservation>();
            var provider = new QualifiedClaimGroup("provider", "Unit " + index + " receives Process.", ClaimQualification.SourceSupported, [Evidence(10)], []);
            claims.Add(provider);
            observations.Add(new("provider", "unit-" + index, "provider", "message", "Process", "controlled-namespace", [], ClaimQualification.SourceSupported, provider.Evidence));
            int ordinal = 0;
            foreach (var edge in edges.Where(value => value.From == index))
            {
                string key = "call-" + edge.To;
                var action = new QualifiedClaimGroup(key, "Unit " + index + " sends Process to unit " + edge.To + ".", ClaimQualification.SourceSupported, [Evidence(20 + ordinal * 10)], []);
                var binding = new QualifiedClaimGroup(key + "-binding", "Static configuration explicitly connects unit " + index + " to unit " + edge.To + ".", ClaimQualification.SourceSupported, [Evidence(25 + ordinal * 10)], [Limitation]);
                claims.Add(action);
                claims.Add(binding);
                observations.Add(new(key, "unit-" + index, "caller", "message", "Process", "controlled-namespace", [], ClaimQualification.SourceSupported, action.Evidence.AddRange(binding.Evidence)));
                ordinal++;
            }
            claims.Add(new("advice", "Controlled advisory, not source proof.", ClaimQualification.Advisory,
                [new(null, null, null, CitedAdvisory.AdvisoryRecordId, CitedAdvisory.ContentDigest)], []));
            claims.Add(new("hypothesis", "Runtime deployment remains a hypothesis.", ClaimQualification.Hypothesis, [], [Limitation]));
            if (unsupportedObservation && index == 0)
                observations.Add(new("unknown-role", "unit-0", "unknown", "message", "Process", null, [], ClaimQualification.Uncertain, provider.Evidence));
            string contentDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new { index, claims, observations }));
            version = version with { DocumentVersionId = new Guid(Convert.FromHexString(contentDigest[..32])) };
            documents.Add(new(version, origin.RepositoryId, [new("operation", 0, "Operation", claims.ToImmutable())], observations.ToImmutable(),
                [new(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest), CitedAdvisory, UncitedAdvisory]));
            units.Add(new(Id(3000 + index), version, "unit-" + index, origin, [new(origin, 1, 1000)]));
            coverage.Add(new(Id(5000 + index), origin, CoverageDisposition.Accepted, null));
        }
        string corpusDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(documents.ToImmutable()));
        var corpus = new ResolutionCorpus(new(new("ControlledFixture/flow-planner", "controlled-target", Id(1), new string('a', 64)),
            new Guid(Convert.FromHexString(corpusDigest[..32])), corpusDigest), documents.ToImmutable());
        var plan = OctoberResolutionPolicy.Plan(corpus);
        Assert.Equal(edges.Length, plan.Inquiries.Length);
        var accepted = ImmutableArray.CreateBuilder<FlowAcceptedResolution>();
        for (int index = 0; index < edges.Length; index++)
        {
            var edge = edges[index];
            var from = corpus.Documents[edge.From];
            var to = corpus.Documents[edge.To];
            string key = "call-" + edge.To;
            var inquiry = plan.Inquiries.Single(value => value.Caller.Document == from.Version && value.Caller.ObservationKey == key);
            ResolutionEvidenceAssertion Assertion(ResolutionAssertionKind kind, ResolutionDocument document, string claimId)
            {
                var claim = document.Sections[0].Claims.Single(value => value.ClaimGroupId == claimId);
                return new(kind, document.Version, "operation", claimId, claim.Evidence[0], claim.Text, "Controlled exact accepted interpretation.");
            }
            var witness = new ResolutionWitness(inquiry.Caller, new(to.Version, "provider"), "Process", "provider", "Process", "message", "controlled-namespace",
                "The accepted configuration connects these exact units.", null,
                [Assertion(ResolutionAssertionKind.CallerAction, from, key), Assertion(ResolutionAssertionKind.ProviderRoleAndOperation, to, "provider"), Assertion(ResolutionAssertionKind.ConnectingBinding, from, key + "-binding")],
                [], "Directed configured relationship.", [Limitation], false, false);
            var outcome = OctoberResolutionPolicy.Validate(corpus, inquiry, witness);
            Assert.Equal(ResolutionOutcomeKind.Supported, outcome.Kind);
            accepted.Add(new(Id(6000 + index), outcome, Id(7000 + index)));
        }
        Input = new(corpus, units.ToImmutable(), accepted.ToImmutable(), roots.Select(index => new FlowRootSelection(units[index].UnitId, units[index].Document, FlowRootKind.SelectedCodeUnit, units[index].SemanticSelector)).ToImmutableArray(), plan.Dispositions, coverage.ToImmutable())
        {
            Contributors = corpus.Documents.SelectMany(document => document.Contributors).Distinct().ToImmutableArray()
        };
    }

    private static Guid Id(int value) => ResolutionCase.Id(value);
}
