using System.Collections.Concurrent;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Source;

internal sealed class RetainedSourceReadContext : ISourceReadContext
{
    private readonly VerifiedRetainedSourceObject _source;
    private readonly SourceAuthorizationSubject _subject;
    private readonly AuthorizedSourceRepository _binding;
    private readonly ISourceRepositoryAuthorizationVerifier _authorization;
    private readonly IRetainedSourceRepository _snapshots;
    private readonly DurableRetainedSourceStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly long _issuedTimestamp;
    private readonly TimeSpan _deadline;
    private readonly int _maximumEntriesPerOperation;
    private readonly int _maximumBytesPerOperation;
    private readonly object _budgetGate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<string, CursorState> _cursors = new(StringComparer.Ordinal);
    private int _remainingOperations;
    private int _remainingMetadataEntries;
    private long _remainingBytes;
    private bool _disposed;

    internal RetainedSourceReadContext(
        VerifiedRetainedSourceObject source,
        SourceAuthorizationSubject subject,
        AuthorizedSourceRepository binding,
        RetainedSourceCapturePolicy policy,
        SourceReadGrantRequest requestedGrant,
        ISourceRepositoryAuthorizationVerifier authorization,
        IRetainedSourceRepository snapshots,
        DurableRetainedSourceStore store,
        TimeProvider timeProvider)
    {
        _source = source;
        _subject = subject;
        _binding = binding;
        _authorization = authorization;
        _snapshots = snapshots;
        _store = store;
        _timeProvider = timeProvider;
        _issuedTimestamp = timeProvider.GetTimestamp();
        _remainingOperations = Lower(requestedGrant.MaximumOperations, policy.MaximumReadOperations);
        _remainingMetadataEntries = Lower(
            requestedGrant.MaximumMetadataEntries,
            policy.MaximumMetadataEntries);
        _remainingBytes = Lower(requestedGrant.MaximumReturnedBytes, policy.MaximumReturnedBytes);
        _maximumEntriesPerOperation = Lower(
            requestedGrant.MaximumEntriesPerOperation,
            policy.MaximumEntriesPerOperation);
        _maximumBytesPerOperation = Lower(
            requestedGrant.MaximumBytesPerOperation,
            policy.MaximumBytesPerOperation);
        _deadline = requestedGrant.Deadline is { } requestedDeadline &&
            requestedDeadline < policy.ReadDeadline
                ? requestedDeadline
                : policy.ReadDeadline;
    }

    public Guid ContextId { get; } = Guid.NewGuid();
    public Guid RepositoryId => _source.Snapshot.RepositoryId;
    public string ProfiledRevision => _source.Snapshot.ProfiledRevision;

    public async Task<SourceEntryResult> GetEntryAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        if (IsDisposed())
        {
            return EntryFailure(SourceReadStatus.Cancelled, "source_cancelled", correlationId);
        }

        if (!IsCanonicalPath(relativePath, allowEmpty: false))
        {
            return EntryFailure(SourceReadStatus.Denied, "source_denied", correlationId);
        }

        if (!TryReserve(metadataEntries: 1, returnedBytes: 0, out SourceBudgetDimension? exhausted))
        {
            return EntryFailure(
                SourceReadStatus.BudgetExceeded,
                BudgetCode(exhausted),
                correlationId);
        }

        SourceReadStatus? unavailable = await RevalidateOperationAsync(cancellationToken);
        if (unavailable is { } status)
        {
            return EntryFailure(status, StatusCode(status), correlationId);
        }

        SourceEntry? entry = FindEntry(relativePath);
        return entry is null
            ? EntryFailure(
                SourceReadStatus.NotFoundOrUnavailable,
                "source_not_found_or_unavailable",
                correlationId)
            : new(
                SourceReadStatus.Succeeded,
                "source_entry_succeeded",
                correlationId,
                RepositoryId,
                ProfiledRevision,
                _source.Snapshot.ManifestSha256,
                entry);
    }

    public async Task<SourceListResult> ListAsync(
        string relativeDirectory,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        if (IsDisposed())
        {
            return ListFailure(SourceReadStatus.Cancelled, "source_cancelled", correlationId);
        }

        if (!IsCanonicalPath(relativeDirectory, allowEmpty: true) || limit <= 0)
        {
            return ListFailure(SourceReadStatus.Denied, "source_denied", correlationId);
        }

        if (limit > _maximumEntriesPerOperation)
        {
            return ListFailure(
                SourceReadStatus.BudgetExceeded,
                "source_budget_entries_per_operation_exceeded",
                correlationId,
                SourceBudgetDimension.EntriesPerOperation);
        }

        if (!TryReserve(limit, returnedBytes: 0, out SourceBudgetDimension? exhausted))
        {
            return ListFailure(
                SourceReadStatus.BudgetExceeded,
                BudgetCode(exhausted),
                correlationId,
                exhausted);
        }

        SourceReadStatus? unavailable = await RevalidateOperationAsync(cancellationToken);
        if (unavailable is { } status)
        {
            return ListFailure(status, StatusCode(status), correlationId);
        }

        int position = 0;
        if (cursor is not null &&
            (!_cursors.TryGetValue(cursor, out CursorState? state) ||
                !StringComparer.Ordinal.Equals(state.RelativeDirectory, relativeDirectory)))
        {
            return ListFailure(SourceReadStatus.Denied, "source_denied", correlationId);
        }
        else if (cursor is not null)
        {
            position = _cursors[cursor].Position;
        }

        IReadOnlyList<ListProjectionItem> children = ImmediateChildren(relativeDirectory);
        ListProjectionItem[] page = [.. children.Skip(position).Take(limit)];
        int nextPosition = position + page.Length;
        string? nextCursor = null;
        if (nextPosition < children.Count)
        {
            nextCursor = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
            _cursors[nextCursor] = new(relativeDirectory, nextPosition);
        }

        return new(
            SourceReadStatus.Succeeded,
            "source_list_succeeded",
            correlationId,
            RepositoryId,
            ProfiledRevision,
            _source.Snapshot.ManifestSha256,
            page.Where(static item => item.Entry is not null)
                .Select(static item => item.Entry!)
                .ToArray(),
            page.Where(static item => item.Limitation is not null)
                .Select(static item => item.Limitation!)
                .ToArray(),
            nextCursor,
            null);
    }

    public Task<SourceBytesResult> ReadBytesAsync(
        string relativePath,
        long byteOffset,
        int maximumBytes,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SourceBytesResult(
            SourceReadStatus.NotFoundOrUnavailable,
            "source_read_unavailable",
            Guid.NewGuid(),
            RepositoryId,
            ProfiledRevision,
            _source.Snapshot.ManifestSha256,
            null,
            ReadOnlyMemory<byte>.Empty,
            null,
            null,
            null,
            null,
            null,
            null));

    public Task<SourceTextResult> ReadTextAsync(
        string relativePath,
        long byteOffset,
        int maximumBytes,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SourceTextResult(
            SourceReadStatus.NotFoundOrUnavailable,
            "source_read_unavailable",
            Guid.NewGuid(),
            RepositoryId,
            ProfiledRevision,
            _source.Snapshot.ManifestSha256,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null));

    public ValueTask DisposeAsync()
    {
        lock (_budgetGate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }
            _disposed = true;
        }
        _lifetime.Cancel();
        _lifetime.Dispose();
        _cursors.Clear();
        return ValueTask.CompletedTask;
    }

    private async Task<SourceReadStatus?> CheckAvailabilityAsync(CancellationToken callCancellation)
    {
        if (_disposed || _lifetime.IsCancellationRequested || callCancellation.IsCancellationRequested)
        {
            return SourceReadStatus.Cancelled;
        }
        if (_timeProvider.GetElapsedTime(_issuedTimestamp) >= _deadline)
        {
            return SourceReadStatus.DeadlineExceeded;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime.Token,
            callCancellation);
        try
        {
            AuthorizedSourceRepository? current = await _authorization.AuthorizeAsync(
                new(
                    _subject,
                    SourceAuthorizationPurpose.Read,
                    RepositoryId,
                    ProfiledRevision),
                linked.Token);
            if (current is null || current != _binding)
            {
                return SourceReadStatus.Denied;
            }

            linked.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            return SourceReadStatus.Cancelled;
        }

        return _timeProvider.GetElapsedTime(_issuedTimestamp) >= _deadline
            ? SourceReadStatus.DeadlineExceeded
            : null;
    }

    private async Task<SourceReadStatus?> RevalidateOperationAsync(
        CancellationToken cancellationToken)
    {
        SourceReadStatus? unavailable = await CheckAvailabilityAsync(cancellationToken);
        if (unavailable is not null)
        {
            return unavailable;
        }

        try
        {
            SourceSnapshot? current = await _snapshots.GetAsync(
                RepositoryId,
                ProfiledRevision,
                cancellationToken);
            if (current is null || !SameSnapshotBinding(current, _source.Snapshot))
            {
                return SourceReadStatus.IntegrityFailure;
            }

            await _store.VerifyManifestBindingAsync(current, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return SourceReadStatus.Cancelled;
        }
        catch (RetainedSourceIntegrityException)
        {
            return SourceReadStatus.IntegrityFailure;
        }
        catch (RetainedSourceStorageException)
        {
            return SourceReadStatus.StorageFailure;
        }

        return await CheckAvailabilityAsync(cancellationToken);
    }

    private static bool SameSnapshotBinding(
        SourceSnapshot current,
        SourceSnapshot opened) =>
        current.Id == opened.Id &&
        current.RepositoryId == opened.RepositoryId &&
        StringComparer.Ordinal.Equals(current.ProfiledRevision, opened.ProfiledRevision) &&
        StringComparer.Ordinal.Equals(current.GitObjectFormat, opened.GitObjectFormat) &&
        StringComparer.Ordinal.Equals(current.ManifestSchemaIdentity, opened.ManifestSchemaIdentity) &&
        StringComparer.Ordinal.Equals(current.ManifestSha256, opened.ManifestSha256) &&
        current.EntryCount == opened.EntryCount &&
        current.TotalBlobBytes == opened.TotalBlobBytes &&
        StringComparer.Ordinal.Equals(current.InternalStorageKey, opened.InternalStorageKey) &&
        current.CompletedAt == opened.CompletedAt;

    private bool TryReserve(
        int metadataEntries,
        int returnedBytes,
        out SourceBudgetDimension? exhausted)
    {
        lock (_budgetGate)
        {
            if (_disposed)
            {
                exhausted = SourceBudgetDimension.Operations;
                return false;
            }
            if (_remainingOperations < 1)
            {
                exhausted = SourceBudgetDimension.Operations;
                return false;
            }
            if (_remainingMetadataEntries < metadataEntries)
            {
                exhausted = SourceBudgetDimension.MetadataEntries;
                return false;
            }
            if (_remainingBytes < returnedBytes)
            {
                exhausted = SourceBudgetDimension.ReturnedBytes;
                return false;
            }
            _remainingOperations--;
            _remainingMetadataEntries -= metadataEntries;
            _remainingBytes -= returnedBytes;
            exhausted = null;
            return true;
        }
    }

    private bool IsDisposed()
    {
        lock (_budgetGate)
        {
            return _disposed;
        }
    }

    private SourceEntry? FindEntry(string relativePath)
    {
        RetainedSourceManifestEntry? manifestEntry = _source.Manifest.Entries
            .FirstOrDefault(entry => StringComparer.Ordinal.Equals(entry.RelativePath, relativePath));
        if (manifestEntry is not null)
        {
            return Project(manifestEntry);
        }

        string prefix = relativePath + "/";
        return _source.Manifest.Entries.Any(entry =>
            entry.RelativePath?.StartsWith(prefix, StringComparison.Ordinal) == true)
                ? Directory(relativePath)
                : null;
    }

    private IReadOnlyList<ListProjectionItem> ImmediateChildren(string relativeDirectory)
    {
        string prefix = relativeDirectory.Length == 0 ? string.Empty : relativeDirectory + "/";
        var children = new Dictionary<string, ListProjectionItem>(StringComparer.Ordinal);
        var limitations = new List<ListProjectionItem>();
        foreach (RetainedSourceManifestEntry manifestEntry in _source.Manifest.Entries)
        {
            string? path = manifestEntry.RelativePath;
            if (path is null)
            {
                if (relativeDirectory.Length == 0)
                {
                    limitations.Add(new(
                        manifestEntry.Ordinal,
                        null,
                        new SourceLimitationSummary(
                            manifestEntry.Ordinal,
                            manifestEntry.LimitationCode ?? "source_path_unsupported")));
                }
                continue;
            }

            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }
            string remainder = path[prefix.Length..];
            int separator = remainder.IndexOf('/');
            string childPath = separator < 0
                ? path
                : prefix + remainder[..separator];
            children.TryAdd(
                childPath,
                new(
                    manifestEntry.Ordinal,
                    separator < 0 ? Project(manifestEntry) : Directory(childPath),
                    null));
        }
        return [.. children.Values.Concat(limitations).OrderBy(static item => item.Ordinal)];
    }

    private static SourceEntry Project(RetainedSourceManifestEntry entry) =>
        new(
            entry.RelativePath!,
            entry.EntryKind switch
            {
                "regularFile" => SourceEntryKind.RegularFile,
                "executableFile" => SourceEntryKind.ExecutableFile,
                "symbolicLink" => SourceEntryKind.SymbolicLink,
                "gitlink" => SourceEntryKind.Gitlink,
                _ => throw new RetainedSourceIntegrityException(),
            },
            entry.ByteLength,
            entry.ContentSha256,
            entry.Capabilities.Select(static capability => capability switch
            {
                "metadata" => SourceEntryCapability.Metadata,
                "byteRange" => SourceEntryCapability.ByteRange,
                "strictUtf8Text" => SourceEntryCapability.StrictUtf8Text,
                _ => throw new RetainedSourceIntegrityException(),
            }).ToArray(),
            entry.LimitationCode);

    private static SourceEntry Directory(string relativePath) =>
        new(relativePath, SourceEntryKind.Directory, null, null, [SourceEntryCapability.Metadata], null);

    private static bool IsCanonicalPath(string? path, bool allowEmpty)
    {
        if (path is null || (!allowEmpty && path.Length == 0) || path.Length > 4_096 ||
            path.StartsWith('/') || path.Contains('\\', StringComparison.Ordinal) ||
            path.Contains(':', StringComparison.Ordinal) || path.Any(char.IsControl))
        {
            return false;
        }
        return path.Length == 0 || path.Split('/').All(static segment => segment is not ("" or "." or ".."));
    }

    private SourceEntryResult EntryFailure(SourceReadStatus status, string code, Guid correlationId) =>
        new(status, code, correlationId, RepositoryId, ProfiledRevision, null, null);

    private SourceListResult ListFailure(
        SourceReadStatus status,
        string code,
        Guid correlationId,
        SourceBudgetDimension? dimension = null) =>
        new(status, code, correlationId, RepositoryId, ProfiledRevision, null, [], [], null, dimension);

    private static string StatusCode(SourceReadStatus status) => status switch
    {
        SourceReadStatus.Denied => "source_denied",
        SourceReadStatus.DeadlineExceeded => "source_deadline_exceeded",
        SourceReadStatus.Cancelled => "source_cancelled",
        SourceReadStatus.IntegrityFailure => "source_integrity_failed",
        SourceReadStatus.StorageFailure => "source_storage_unavailable",
        _ => "source_not_found_or_unavailable",
    };

    private static string BudgetCode(SourceBudgetDimension? dimension) => dimension switch
    {
        SourceBudgetDimension.Operations => "source_budget_operations_exceeded",
        SourceBudgetDimension.MetadataEntries => "source_budget_metadata_entries_exceeded",
        SourceBudgetDimension.ReturnedBytes => "source_budget_returned_bytes_exceeded",
        _ => "source_budget_exceeded",
    };

    private static int Lower(int? requested, int maximum) =>
        requested is > 0 ? Math.Min(requested.Value, maximum) : maximum;
    private static long Lower(long? requested, long maximum) =>
        requested is > 0 ? Math.Min(requested.Value, maximum) : maximum;

    private sealed record CursorState(string RelativeDirectory, int Position);
    private sealed record ListProjectionItem(
        long Ordinal,
        SourceEntry? Entry,
        SourceLimitationSummary? Limitation);
}
