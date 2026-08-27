using System.Text.RegularExpressions;

namespace Vulgata.Tests.ReleaseGate;

internal sealed record Story125RunIdentity(string DatabaseName);

internal static partial class Story125ReleaseHarness
{
    private const string DatabasePrefix = "vulgata_story125_";

    internal static Story125RunIdentity CreateRunIdentity() =>
        new(DatabasePrefix + Guid.NewGuid().ToString("N"));

    internal static bool IsOwnedDatabaseName(string? databaseName) =>
        databaseName is not null && OwnedDatabaseNameRegex().IsMatch(databaseName);

    [GeneratedRegex("^vulgata_story125_[a-z0-9_]{8,48}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnedDatabaseNameRegex();
}
