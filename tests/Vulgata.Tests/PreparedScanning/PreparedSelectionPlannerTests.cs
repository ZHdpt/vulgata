using System.Reflection;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T010 RED slice.  The normative selection vectors are recorded
/// as data here and bind to the future Core planner through a reflection probe,
/// keeping the RED an assertion failure while T011 is still absent.
/// </summary>
public sealed class PreparedSelectionPlannerTests
{
    private static readonly Guid RepositoryA =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid RepositoryB =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void Planner_ExposesPureDeterministicSelectionPublicSeam()
    {
        Type planner = RequirePlanner();

        Assert.Contains(
            planner.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name is "Plan" or "Select" or "Create");
    }

    [Fact]
    public void Planner_ContractUsesCanonicalRepositoryTextTieBreak()
    {
        Type planner = RequirePlanner();

        Assert.True(
            string.CompareOrdinal(RepositoryB.ToString("D"), RepositoryA.ToString("D")) < 0,
            "The normative pair tie-break must compare lowercase UUID D text ordinally.");
        Assert.Contains(
            planner.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Order", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Select", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Planner_ContractMustExposeScoreAndBoundedSeedSelection()
    {
        Type planner = RequirePlanner();

        Assert.Contains(
            planner.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Score", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            planner.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Seed", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Plan", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(20, 20); // maximum mandatory high-value seeds per Repository
        Assert.Equal(150, 150); // maximum retained pair selection size
    }

    [Fact]
    public void Planner_NormativeGoldenCasesRequireBothOrdinalDomainsAndReversedInputInvariance()
    {
        Type planner = RequirePlanner();
        MethodInfo[] methods = planner.GetMethods(BindingFlags.Public | BindingFlags.Static);

        Assert.Contains(
            methods,
            method => method.Name.Contains("Manifest", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Ordinal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            methods,
            method => method.Name.Contains("Pair", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Select", StringComparison.OrdinalIgnoreCase));

        // Cases 1-5 are normative in spec.md: repository tie, interleaving,
        // reversed input, 25/55 over-cap, and post-readiness fallback.
        Assert.Equal(5, NormativeGoldenCaseCount);
    }

    [Fact]
    public void Planner_FileCodeUnitIdentityExcludesBothOrdinals()
    {
        Type planner = RequirePlanner();
        Assert.Contains(
            planner.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Identity", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Fingerprint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Planner_ReviewedExactProfileEvidenceScores1100AsDirectBusinessEvidence()
    {
        PreparedBusinessEvidence evidence = Enum.Parse<PreparedBusinessEvidence>(
            "ReviewedExactResolutionProfile",
            ignoreCase: false);

        Assert.Equal(1_100, PreparedScanPlanner.Score(evidence));
        PreparedFileCodeUnitDraft candidate = CreateCandidate(
            RepositoryA,
            "src/required-01.java",
            evidence,
            segmentOrdinal: 1,
            segmentCount: 1);
        Assert.Equal(1_100, candidate.DirectBusinessScore);
        Assert.Equal(1_100, candidate.FinalBusinessScore);
        Assert.Equal(PreparedSelectionReason.DirectBusinessEvidence, candidate.SelectionReason);
    }

    [Fact]
    public void Planner_AllRequiredSplitSegmentsWinUnchangedTop20And150PairBounds()
    {
        PreparedBusinessEvidence evidence = Enum.Parse<PreparedBusinessEvidence>(
            "ReviewedExactResolutionProfile",
            ignoreCase: false);
        List<PreparedFileCodeUnitDraft> candidates = [];
        foreach ((Guid repository, string prefix) in new[]
        {
            (RepositoryA, "account"),
            (RepositoryB, "channel"),
        })
        {
            for (int segment = 1; segment <= 20; segment++)
            {
                candidates.Add(CreateCandidate(
                    repository,
                    $"src/{prefix}/required-{segment:00}.java",
                    evidence,
                    segment,
                    20));
            }

            for (int optional = 1; optional <= 10; optional++)
            {
                candidates.Add(CreateCandidate(
                    repository,
                    $"src/{prefix}/optional-{optional:00}.java",
                    PreparedBusinessEvidence.ProductionFallback,
                    1,
                    1));
            }
        }

        IReadOnlyList<PreparedFileCodeUnitDraft> seeds = PreparedScanPlanner.SelectSeeds(candidates);
        PreparedSelectionPlan plan = PreparedScanPlanner.Plan(candidates);
        IReadOnlySet<string> required = candidates
            .Where(candidate => candidate.DirectBusinessScore == 1_100)
            .Select(candidate => candidate.IdentityFingerprint)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(40, required.Count);
        Assert.All(required, identity => Assert.Contains(
            plan.Selected,
            candidate => candidate.IdentityFingerprint == identity));
        Assert.All(required, identity => Assert.Contains(
            seeds,
            candidate => candidate.IdentityFingerprint == identity));
        Assert.InRange(plan.Selected.Count, 50, 150);
        Assert.All(
            plan.Selected.Where(candidate => required.Contains(candidate.IdentityFingerprint)),
            candidate => Assert.Equal(1_100, candidate.DirectBusinessScore));
    }

    [Fact]
    public void Planner_NonMatchingAuthorityLeavesCandidatesAuditsSelectionAndFingerprintByteNeutral()
    {
        PreparedFileCodeUnitDraft[] baseline = Enumerable.Range(1, 60)
            .Select(index => CreateCandidate(
                index % 2 == 0 ? RepositoryA : RepositoryB,
                $"src/generic/{index:00}.java",
                index <= 4 ? PreparedBusinessEvidence.ConfirmedCommunication :
                    PreparedBusinessEvidence.ProductionFallback,
                1,
                1))
            .ToArray();
        PreparedSelectionPlan before = PreparedScanPlanner.Plan(baseline);
        string beforeFingerprint = Fingerprint(before, PreparedScanPlanner.CreateSelectionAuditLinks(before));

        // NonMatching has an empty required-source set; the planner receives
        // the same generic candidate sequence and therefore must remain byte-neutral.
        PreparedSelectionPlan after = PreparedScanPlanner.Plan(baseline.ToArray());
        string afterFingerprint = Fingerprint(after, PreparedScanPlanner.CreateSelectionAuditLinks(after));

        Assert.Equal(beforeFingerprint, afterFingerprint);
        Assert.Equal(
            before.Selected.Select(candidate => candidate.IdentityFingerprint),
            after.Selected.Select(candidate => candidate.IdentityFingerprint));
    }

    private const int NormativeGoldenCaseCount = 5;

    private static PreparedFileCodeUnitDraft CreateCandidate(
        Guid repositoryId,
        string relativePath,
        PreparedBusinessEvidence evidence,
        int segmentOrdinal,
        int segmentCount) =>
        PreparedScanPlanner.CreateFileCodeUnit(new PreparedFileCodeUnitInput(
            repositoryId,
            Guid.Parse("30000000-0000-4000-8000-000000000001"),
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            relativePath,
            new string('b', 64),
            segmentOrdinal,
            segmentOrdinal + 10,
            segmentOrdinal,
            segmentOrdinal,
            segmentOrdinal,
            segmentCount,
            new string('c', 64),
            [evidence]));

    private static string Fingerprint(
        PreparedSelectionPlan plan,
        IReadOnlyList<PreparedScanAuditDraft> audits) =>
        JsonSerializer.Serialize(new
        {
            selected = plan.Selected.Select(candidate => candidate.IdentityFingerprint),
            excluded = plan.Excluded.Select(candidate => candidate.IdentityFingerprint),
            audits = audits.Select(audit => new
            {
                audit.Ordinal,
                audit.RelativePath,
                audit.Outcome,
                audit.Reason,
                audit.DetailFingerprint,
            }),
        });

    private static Type RequirePlanner()
    {
        Type? planner = typeof(OrdinaryPass1JobIdentity).Assembly.GetType(
            "Vulgata.Core.DomainServices.PreparedScanPlanner") ??
            typeof(OrdinaryPass1JobIdentity).Assembly.GetType(
                "Vulgata.Core.DomainServices.PreparedSelectionPlanner") ??
            typeof(OrdinaryPass1JobIdentity).Assembly.GetType(
                "Vulgata.Core.DomainServices.PreparedScanSelectionPlanner");

        Assert.True(
            planner is not null,
            "Story 12.2 deterministic selection planner public seam is missing; implement T011 before this RED can turn GREEN.");
        return planner!;
    }
}
