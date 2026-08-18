using System.Text;

namespace Vulgata.Tests.AnalysisRuntime;

internal sealed class InMemoryFileSystemDouble
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public bool ObservedCancellation { get; private set; }

    public void AddUtf8(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        _files[Normalize(path)] = Encoding.UTF8.GetBytes(content);
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ObserveCancellation(cancellationToken);

        if (!_files.TryGetValue(Normalize(path), out byte[]? bytes))
        {
            throw new FileNotFoundException("The requested test file was not configured.", path);
        }

        return Task.FromResult(bytes.ToArray());
    }

    public Task<IReadOnlyList<string>> EnumerateFilesAsync(string root, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ObserveCancellation(cancellationToken);

        string normalizedRoot = Normalize(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        IReadOnlyList<string> paths = _files.Keys
            .Where(path => path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult(paths);
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    private void ObserveCancellation(CancellationToken cancellationToken)
    {
        ObservedCancellation = cancellationToken.IsCancellationRequested;
        cancellationToken.ThrowIfCancellationRequested();
    }
}
