namespace Vulgata.Tests.CodeGraphQualification;

public static class QualificationSyntheticChildBridge
{
    public static async Task ClaimAndCompleteAsync(
        string attemptsRoot,
        IDictionary<string, string> environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptsRoot);
        ArgumentNullException.ThrowIfNull(environment);
        Dictionary<string, string?> values = environment.ToDictionary(
            item => item.Key,
            item => (string?)item.Value,
            StringComparer.Ordinal);
        bool executed = await ProviderQualificationEntry.ExecuteAsync(
            attemptsRoot,
            values,
            () => DateTimeOffset.UtcNow,
            (_, _) => Task.FromResult(
                new ProviderQualificationTerminal("failed", "synthetic-child-classification")));
        if (!executed)
        {
            throw new InvalidOperationException("The synthetic child bridge remained inert.");
        }
    }
}
