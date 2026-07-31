using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Git;

public sealed class ScanCandidateEnumerator : IScanCandidateEnumerator
{
    private static readonly string[] _vendoredSegments =
        ["/vendor/", "/vendors/", "/node_modules/", "/packages/", "/third_party/", "/third-party/"];

    public async Task<IReadOnlyList<ScanSourceCandidate>> EnumerateAsync(
        string workingCopyPath,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(workingCopyPath);
        List<string> paths = EnumerateCandidateFiles(root)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var results = new List<ScanSourceCandidate>(paths.Count);
        int eligibleOrdinal = 0;
        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? category = await GetFilterCategoryAsync(root, path, cancellationToken);
            int group = category is null ? eligibleOrdinal++ / 1_000 : 0;
            results.Add(new ScanSourceCandidate(path, category is not null, category ?? "Eligible", group));
        }

        return results;
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!path.Equals(Path.Combine(root, ".git"), StringComparison.OrdinalIgnoreCase) &&
                        (attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(path);
                    }
                }
                else
                {
                    yield return path;
                }
            }
        }
    }

    private static async Task<string?> GetFilterCategoryAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
        {
            return "ReparsePoint";
        }

        string normalized = "/" + relativePath.ToLowerInvariant();
        if (_vendoredSegments.Any(normalized.Contains))
        {
            return "Vendored";
        }

        string fileName = Path.GetFileName(relativePath);
        if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".pb.cs", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".pb.go", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("thrift", StringComparison.OrdinalIgnoreCase))
        {
            return "Generated";
        }

        byte[] prefix = new byte[8_192];
        await using FileStream stream = File.OpenRead(fullPath);
        int read = await stream.ReadAsync(prefix, cancellationToken);
        if (prefix.AsSpan(0, read).Contains((byte)0))
        {
            return "Binary";
        }

        if (fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase))
        {
            return "MinifiedJavaScript";
        }

        return null;
    }
}
