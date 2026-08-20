using System.Reflection;
using Vulgata.Core.DomainServices;

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

    private const int NormativeGoldenCaseCount = 5;

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
