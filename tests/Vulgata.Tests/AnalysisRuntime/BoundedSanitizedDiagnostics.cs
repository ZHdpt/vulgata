using System.Text.RegularExpressions;

namespace Vulgata.Tests.AnalysisRuntime;

internal static partial class BoundedSanitizedDiagnostics
{
    public const int DefaultMaximumLength = 256;

    public static string Create(string? diagnostic, int maximumLength = DefaultMaximumLength)
    {
        if (maximumLength is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        }

        string sanitized = string.IsNullOrWhiteSpace(diagnostic)
            ? "No diagnostic details were supplied."
            : diagnostic.ReplaceLineEndings(" ").Trim();
        sanitized = SecretAssignment().Replace(sanitized, "$1=[redacted]");
        sanitized = WindowsPath().Replace(sanitized, "[path]");
        sanitized = UnixPath().Replace(sanitized, "[path]");
        sanitized = RepeatedWhitespace().Replace(sanitized, " ");

        return sanitized.Length <= maximumLength
            ? sanitized
            : string.Concat(sanitized.AsSpan(0, maximumLength - 1), "…");
    }

    [GeneratedRegex(@"(?i)\b(api[-_]?key|authorization|password|token)\s*[:=]\s*([^\s;,]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignment();

    [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^\s\""']+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPath();

    [GeneratedRegex(@"(?<![\w.])/(?:[^\s/]+/)*[^\s\""']+", RegexOptions.CultureInvariant)]
    private static partial Regex UnixPath();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedWhitespace();
}
