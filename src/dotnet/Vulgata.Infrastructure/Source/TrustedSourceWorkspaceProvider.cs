using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Source;

internal sealed class TrustedSourceWorkspaceOptions
{
    internal TrustedSourceWorkspaceOptions(string workspaceRoot, string sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(sourceRoot))
        {
            throw new ArgumentException("Trusted source workspace roots are required.");
        }

        WorkspaceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        string normalizedSourceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot));
        if (Contains(WorkspaceRoot, normalizedSourceRoot) ||
            Contains(normalizedSourceRoot, WorkspaceRoot) ||
            IsReparsePoint(WorkspaceRoot))
        {
            throw new ArgumentException("Trusted source workspace root is invalid.");
        }
    }

    internal string WorkspaceRoot { get; }

    private static bool Contains(string root, string candidate)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(root, candidate, comparison))
        {
            return true;
        }

        string prefix = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, comparison);
    }

    internal static bool IsReparsePoint(string path) =>
        Directory.Exists(path) &&
        File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
}

internal sealed class TrustedSourceWorkspaceProvider(
    TrustedSourceWorkspaceOptions options,
    IUserContext user,
    ISourceRepositoryAuthorizationVerifier authorization,
    IRetainedSourceRepository snapshots,
    DurableRetainedSourceStore store) : ITrustedSourceWorkspaceProvider
{
    public async Task<TrustedSourceWorkspaceResult> AcquireAsync(
        VerifiedRetainedSourceObject source,
        TrustedSourceWorkspacePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        string? workspaceRoot = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (purpose != TrustedSourceWorkspacePurpose.SourceAnalysis)
            {
                return Failure(
                    TrustedSourceWorkspaceStatus.Unsupported,
                    "trusted_source_workspace_unsupported",
                    correlationId);
            }

            SourceSnapshot supplied = source.Snapshot;
            var request = new SourceAuthorizationRequest(
                new SourceAuthorizationSubject(user.UserId),
                SourceAuthorizationPurpose.Read,
                supplied.RepositoryId,
                supplied.ProfiledRevision);
            AuthorizedSourceRepository? initial = await authorization.AuthorizeAsync(
                request,
                cancellationToken);
            if (initial is null)
            {
                return Failure(
                    TrustedSourceWorkspaceStatus.Denied,
                    "trusted_source_workspace_denied",
                    correlationId);
            }

            SourceSnapshot? current = await snapshots.GetAsync(
                supplied.RepositoryId,
                supplied.ProfiledRevision,
                cancellationToken);
            if (current is null || !SameSnapshot(current, supplied))
            {
                return Failure(
                    TrustedSourceWorkspaceStatus.IntegrityFailure,
                    "trusted_source_workspace_integrity_failed",
                    correlationId);
            }

            VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
                current,
                cancellationToken);
            IReadOnlyList<WorkspaceEntry> entries = Preflight(verified.Manifest);
            workspaceRoot = CreateWorkspaceRoot(options.WorkspaceRoot);
            foreach (WorkspaceEntry workspaceEntry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    string destination = ResolveContained(
                        workspaceRoot,
                        workspaceEntry.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    await using (var destinationStream = new FileStream(
                        destination,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        65_536,
                        FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        await store.CopyVerifiedBlobToAsync(
                            verified,
                            workspaceEntry.ManifestEntry,
                            destinationStream,
                            cancellationToken);
                    }

                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(
                            destination,
                            workspaceEntry.IsExecutable
                                ? UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                  UnixFileMode.UserExecute | UnixFileMode.GroupRead |
                                  UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                                  UnixFileMode.OtherExecute
                                : UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                  UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                    }
                }
                catch (PathTooLongException)
                {
                    throw new TrustedSourceWorkspaceUnsupportedException();
                }
                catch (IOException exception) when (
                    OperatingSystem.IsWindows() &&
                    exception.HResult == unchecked((int)0x8007007B))
                {
                    throw new TrustedSourceWorkspaceUnsupportedException();
                }
            }

            SourceSnapshot? finalSnapshot = await snapshots.GetAsync(
                current.RepositoryId,
                current.ProfiledRevision,
                cancellationToken);
            if (finalSnapshot is null || !SameSnapshot(finalSnapshot, current))
            {
                return Failure(
                    TrustedSourceWorkspaceStatus.IntegrityFailure,
                    "trusted_source_workspace_integrity_failed",
                    correlationId);
            }
            await store.VerifyManifestBindingAsync(finalSnapshot, cancellationToken);

            AuthorizedSourceRepository? finalAuthorization = await authorization.AuthorizeAsync(
                request,
                cancellationToken);
            if (finalAuthorization is null || finalAuthorization != initial)
            {
                return Failure(
                    TrustedSourceWorkspaceStatus.Denied,
                    "trusted_source_workspace_denied",
                    correlationId);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var lease = new TrustedSourceWorkspaceLease(workspaceRoot, current);
            workspaceRoot = null;
            return new(
                TrustedSourceWorkspaceStatus.Succeeded,
                "trusted_source_workspace_acquired",
                correlationId,
                lease);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                TrustedSourceWorkspaceStatus.Cancelled,
                "trusted_source_workspace_cancelled",
                correlationId);
        }
        catch (TrustedSourceWorkspaceUnsupportedException)
        {
            return Failure(
                TrustedSourceWorkspaceStatus.Unsupported,
                "trusted_source_workspace_unsupported",
                correlationId);
        }
        catch (RetainedSourceIntegrityException)
        {
            return Failure(
                TrustedSourceWorkspaceStatus.IntegrityFailure,
                "trusted_source_workspace_integrity_failed",
                correlationId);
        }
        catch
        {
            return Failure(
                TrustedSourceWorkspaceStatus.StorageFailure,
                "trusted_source_workspace_storage_unavailable",
                correlationId);
        }
        finally
        {
            if (workspaceRoot is not null)
            {
                DurableRetainedSourceStore.TryDeleteOwnedOperation(workspaceRoot);
            }
        }
    }

    private static IReadOnlyList<WorkspaceEntry> Preflight(
        RetainedSourceManifestDocument manifest)
    {
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var files = new HashSet<string>(comparer);
        var directories = new Dictionary<string, string>(comparer);
        var result = new List<WorkspaceEntry>(manifest.Entries.Count);
        foreach (RetainedSourceManifestEntry entry in manifest.Entries)
        {
            if (entry.RelativePath is null ||
                entry.EntryKind is not ("regularFile" or "executableFile") ||
                entry.StorageBlobKey is null ||
                entry.ContentSha256 is null ||
                entry.ByteLength is null)
            {
                throw new TrustedSourceWorkspaceUnsupportedException();
            }

            string[] segments = entry.RelativePath.Split('/');
            if (segments.Any(static segment => !IsHostRepresentableSegment(segment)))
            {
                throw new TrustedSourceWorkspaceUnsupportedException();
            }

            string logicalPath = string.Join('/', segments);
            if (directories.ContainsKey(logicalPath) || !files.Add(logicalPath))
            {
                throw new TrustedSourceWorkspaceUnsupportedException();
            }

            string parent = string.Empty;
            for (int index = 0; index < segments.Length - 1; index++)
            {
                parent = index == 0 ? segments[index] : $"{parent}/{segments[index]}";
                if (files.Contains(parent))
                {
                    throw new TrustedSourceWorkspaceUnsupportedException();
                }
                if (directories.TryGetValue(parent, out string? existingParent) &&
                    !StringComparer.Ordinal.Equals(existingParent, parent))
                {
                    throw new TrustedSourceWorkspaceUnsupportedException();
                }
                directories.TryAdd(parent, parent);
            }

            result.Add(new(
                entry,
                logicalPath,
                entry.EntryKind == "executableFile"));
        }

        return result;
    }

    private static bool IsHostRepresentableSegment(string segment)
    {
        if (segment.Length == 0 || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        if (segment.EndsWith(' ') || segment.EndsWith('.'))
        {
            return false;
        }
        string stem = segment.Split('.')[0];
        return !stem.Equals("CON", StringComparison.OrdinalIgnoreCase) &&
            !stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) &&
            !stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) &&
            !stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) &&
            !(stem.Length == 4 &&
              (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
               stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
              stem[3] is >= '1' and <= '9');
    }

    private static string CreateWorkspaceRoot(string configuredRoot)
    {
        if (TrustedSourceWorkspaceOptions.IsReparsePoint(configuredRoot))
        {
            throw new RetainedSourceStorageException();
        }
        Directory.CreateDirectory(configuredRoot);
        if (TrustedSourceWorkspaceOptions.IsReparsePoint(configuredRoot))
        {
            throw new RetainedSourceStorageException();
        }

        string workspaceRoot = ResolveContained(configuredRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        return workspaceRoot;
    }

    private static string ResolveContained(string root, string relativePath)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string candidate = Path.GetFullPath(Path.Combine(
            normalizedRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = normalizedRoot + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!candidate.StartsWith(prefix, comparison))
        {
            throw new TrustedSourceWorkspaceUnsupportedException();
        }
        return candidate;
    }

    private static bool SameSnapshot(SourceSnapshot left, SourceSnapshot right) =>
        left.Id == right.Id &&
        left.RepositoryId == right.RepositoryId &&
        StringComparer.Ordinal.Equals(left.ProfiledRevision, right.ProfiledRevision) &&
        StringComparer.Ordinal.Equals(left.GitObjectFormat, right.GitObjectFormat) &&
        StringComparer.Ordinal.Equals(left.ManifestSha256, right.ManifestSha256) &&
        left.EntryCount == right.EntryCount &&
        left.TotalBlobBytes == right.TotalBlobBytes &&
        StringComparer.Ordinal.Equals(left.InternalStorageKey, right.InternalStorageKey) &&
        left.CompletedAt == right.CompletedAt;

    private static TrustedSourceWorkspaceResult Failure(
        TrustedSourceWorkspaceStatus status,
        string code,
        Guid correlationId) =>
        new(status, code, correlationId, null);

    private sealed record WorkspaceEntry(
        RetainedSourceManifestEntry ManifestEntry,
        string RelativePath,
        bool IsExecutable);
}

internal sealed class TrustedSourceWorkspaceLease(
    string rootPath,
    SourceSnapshot snapshot) : ITrustedSourceWorkspaceLease
{
    private int _disposed;

    public string RootPath { get; } = rootPath;
    public Guid RepositoryId { get; } = snapshot.RepositoryId;
    public string ProfiledRevision { get; } = snapshot.ProfiledRevision;
    public string ManifestSha256 { get; } = snapshot.ManifestSha256;

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            DurableRetainedSourceStore.TryDeleteOwnedOperation(RootPath);
        }
        return ValueTask.CompletedTask;
    }
}

internal sealed class TrustedSourceWorkspaceUnsupportedException : Exception
{
}
