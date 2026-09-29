using Vulgata.Tests.TestInfrastructure;
using Xunit;

namespace Vulgata.Tests.TestInfrastructure;

/// <summary>Selection is independent of ambient credentials and cannot be enabled by a test filter.</summary>
public static class SuiteAuthorization
{
    internal static string ValidateMetadata(IEnumerable<FactAttribute> attributes, IReadOnlyCollection<string> suites)
    {
        FactAttribute[] facts = attributes.ToArray();
        string? guardedSuite = facts.Length == 1 ? facts[0] switch
        {
            SuiteFactAttribute fact => fact.Suite,
            SuiteTheoryAttribute theory => theory.Suite,
            _ => null,
        } : null;
        if (suites.Count != 1 || guardedSuite is null || !suites.Contains(guardedSuite, StringComparer.Ordinal) ||
            guardedSuite is not ("SelfContained" or "PostgreSql" or "LocalTools" or "External" or "E2E" or "HistoricalBoundary"))
            throw new InvalidOperationException("Test must have exactly one guarded Fact/Theory whose suite matches its Suite trait.");
        return guardedSuite;
    }
    public static bool IsEnabled(string suite) => suite == "SelfContained" ||
        string.Equals(Environment.GetEnvironmentVariable("VULGATA_TEST_SUITE"), suite, StringComparison.Ordinal);

    public static string? DisabledReason(string suite) => IsEnabled(suite) ? null :
        $"Suite {suite} requires explicit eng/test.ps1 -Suite {suite} execution.";
}

public class SuiteFactAttribute(string suite) : FactAttribute
{
    public string Suite { get; } = suite;
    public sealed override string? Skip
    {
        get => SuiteAuthorization.DisabledReason(suite) ?? base.Skip;
        set => base.Skip = value;
    }
}

public class SuiteTheoryAttribute(string suite) : TheoryAttribute
{
    public string Suite { get; } = suite;
    public sealed override string? Skip
    {
        get => SuiteAuthorization.DisabledReason(suite) ?? base.Skip;
        set => base.Skip = value;
    }
}
