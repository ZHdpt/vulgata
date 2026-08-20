namespace Vulgata.Infrastructure.Git;

public sealed record PreparedScanCandidate(
    string RelativePath,
    string? Language,
    bool IsEligible,
    string Outcome);

/// <summary>Enumerates a closed, canonical Java/XML inventory from one contained snapshot.</summary>
public sealed class PreparedScanCandidateEnumerator
{
    private static readonly HashSet<string> _excludedSegments = new(
        [
            ".git", ".idea", ".vscode", ".settings", ".gradle", ".mvn", "target",
            "build", "out", "dist", "generated", "node_modules", "vendor", "vendors",
            "packages", "third_party", "third-party",
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> _testSegments = new(
        ["test", "tests", "fixture", "fixtures"],
        StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> ExcludedSegments => _excludedSegments;

    public IReadOnlySet<string> TestSegments => _testSegments;

    public Task<IReadOnlyList<PreparedScanCandidate>> EnumerateAsync(
        string snapshotRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(snapshotRoot);
        if (!Directory.Exists(root) || IsReparsePoint(root))
        {
            throw new InvalidDataException("The prepared snapshot root is unavailable or unsafe.");
        }

        var candidates = new List<PreparedScanCandidate>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileAttributes attributes = File.GetAttributes(entry);
                string relative = RequireCanonicalPath(root, entry);
                if (!paths.Add(relative))
                {
                    throw new InvalidDataException("The snapshot contains a case-insensitive path collision.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        candidates.Add(new(relative, null, false, "LinkOrReparsePoint"));
                    }
                    else
                    {
                        pending.Push(entry);
                    }

                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    candidates.Add(new(relative, null, false, "LinkOrReparsePoint"));
                    continue;
                }

                if ((attributes & FileAttributes.Device) != 0)
                {
                    candidates.Add(new(relative, null, false, "NonRegularEntry"));
                    continue;
                }

                string? language = Path.GetExtension(relative) switch
                {
                    string extension when extension.Equals(".java", StringComparison.OrdinalIgnoreCase) => "Java",
                    string extension when extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) => "XML",
                    _ => null,
                };
                string[] segments = relative.Split('/');
                bool excluded = segments.Any(_excludedSegments.Contains);
                bool test = IsTestPath(segments);
                string outcome = language is null
                    ? "UnsupportedExtension"
                    : excluded
                        ? "ExcludedPathSegment"
                        : test
                            ? "ExcludedTestPath"
                            : "EligibleProductionSource";
                candidates.Add(new(relative, language, outcome == "EligibleProductionSource", outcome));
            }
        }

        return Task.FromResult<IReadOnlyList<PreparedScanCandidate>>(
            candidates.OrderBy(candidate => candidate.RelativePath, StringComparer.Ordinal).ToArray());
    }

    private static bool IsTestPath(string[] segments)
    {
        if (segments.Any(_testSegments.Contains))
        {
            return true;
        }

        for (int index = 0; index + 1 < segments.Length; index++)
        {
            if (segments[index].Equals("src", StringComparison.OrdinalIgnoreCase) &&
                segments[index + 1].Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string RequireCanonicalPath(string root, string entry)
    {
        string full = Path.GetFullPath(entry);
        string prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A prepared candidate escaped the snapshot.");
        }

        string relative = Path.GetRelativePath(root, full).Replace('\\', '/');
        string[] segments = relative.Split('/');
        if (relative.Length is 0 or > 2048 || Path.IsPathFullyQualified(relative) ||
            relative[0] == '/' || relative.Contains('\\') || relative.Any(char.IsControl) ||
            segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidDataException("A prepared candidate path is unsafe.");
        }

        return relative;
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}
