using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

internal sealed class PreparedSourceReadContext : IPreparedSourceReadContext
{
    private readonly SafeFileHandle _rootHandle;
    private readonly PreparedSourceApproval _approval;
    private readonly IPreparedSourceCurrentAuthorityVerifier _authority;
    private readonly long _deadlineStarted;
    private readonly TimeSpan _remainingDeadline;
    private readonly CancellationTokenSource _disposalCancellation = new();
    private readonly CancellationToken _disposalToken;
    private readonly SemaphoreSlim _listGate = new(1, 1);
    private readonly ConcurrentDictionary<string, ListCursor> _listCursors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SearchCursor> _searchCursors = new(StringComparer.Ordinal);
    private readonly object _budgetGate = new();
    private int _remainingOperations;
    private int _remainingMetadataEntries;
    private long _remainingExaminedBytes;
    private long _remainingReturnedBytes;
    private int _closedOutcome;
    private int _stoppedOutcome;
    private int _disposed;

    internal PreparedSourceReadContext(
        PreparedSourceApproval approval,
        SafeFileHandle rootHandle,
        IPreparedSourceCurrentAuthorityVerifier authority,
        PreparedSourceGrantLimits limits,
        TimeSpan remainingDeadline)
    {
        _approval = approval;
        _rootHandle = rootHandle;
        _authority = authority;
        _deadlineStarted = Stopwatch.GetTimestamp();
        _remainingDeadline = remainingDeadline;
        _disposalToken = _disposalCancellation.Token;
        _remainingOperations = limits.MaximumOperations;
        _remainingMetadataEntries = limits.MaximumMetadataEntries;
        _remainingExaminedBytes = limits.MaximumExaminedBytes;
        _remainingReturnedBytes = limits.MaximumReturnedBytes;
    }

    public Guid RepositoryId => _approval.RepositoryId;

    public PreparedSourceContextLifetime Lifetime =>
        Volatile.Read(ref _disposed) != 0
            ? PreparedSourceContextLifetime.Disposed
            : Volatile.Read(ref _closedOutcome) != 0
                ? PreparedSourceContextLifetime.AuthorityClosed
                : Volatile.Read(ref _stoppedOutcome) != 0 || DeadlineElapsed
                    ? PreparedSourceContextLifetime.ExpiredOrCancelled
                : PreparedSourceContextLifetime.Open;

    private bool DeadlineElapsed => Stopwatch.GetElapsedTime(_deadlineStarted) >= _remainingDeadline;

    public async Task<PreparedSourceResult> ListAsync(
        PreparedSourceListRequest request,
        CancellationToken cancellationToken = default)
    {
        PreparedSourceOperation operation = PreparedSourceOperation.List;
        if (request is null ||
            !PreparedSourceTraversal.IsCanonicalRelativePath(request.RelativeDirectory, allowEmpty: true))
        {
            return Failure(operation, PreparedSourceOutcome.Denied);
        }

        if (request.PageSize is < 1 or > PreparedSourceCallLimits.MaximumListEntries)
        {
            return Failure(operation, PreparedSourceOutcome.Denied);
        }

        PreparedSourceOutcome? authorization = await CheckAuthorityAsync(cancellationToken);
        if (authorization is { } denied)
        {
            return Failure(operation, denied);
        }

        try
        {
            await _listGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure(operation, PreparedSourceOutcome.Cancelled);
        }

        try
        {
            ListCursor? cursor = null;
            if (request.Continuation is not null &&
                (!_listCursors.TryRemove(request.Continuation, out cursor) ||
                 cursor.Approval != _approval ||
                 cursor.Operation != operation ||
                 cursor.RelativeDirectory != request.RelativeDirectory ||
                 cursor.PageSize != request.PageSize))
            {
                return Failure(operation, PreparedSourceOutcome.Denied);
            }

            if (!ReserveOperation())
            {
                return Limited(operation, PreparedSourceStoppingLimit.GrantOperations);
            }
            if (request.RelativeDirectory.Length != 0)
            {
                return Failure(operation, PreparedSourceOutcome.Incomplete);
            }

            if (_remainingMetadataEntries == 0)
            {
                return Limited(operation, PreparedSourceStoppingLimit.GrantMetadataEntries);
            }

            PreparedSourceTraversal.DirectoryIdentity? directoryBefore =
                PreparedSourceTraversal.ObserveDirectory(_rootHandle);
            if (directoryBefore is null)
            {
                return Failure(operation, PreparedSourceOutcome.Incomplete);
            }

            (PreparedSourceTraversal.OpenStatus status,
                IReadOnlyList<PreparedSourceTraversal.ListedEntry> observed,
                bool complete) = PreparedSourceTraversal.ListImmediate(
                    _rootHandle,
                    _remainingMetadataEntries);
            _remainingMetadataEntries -= observed.Count;
            PreparedSourceTraversal.DirectoryIdentity? directoryAfter =
                PreparedSourceTraversal.ObserveDirectory(_rootHandle);
            if (directoryAfter is null || directoryAfter != directoryBefore ||
                (cursor is not null && cursor.Directory != directoryBefore))
            {
                return Failure(operation, PreparedSourceOutcome.ChangedInput);
            }

            if (status != PreparedSourceTraversal.OpenStatus.Opened)
            {
                return Failure(operation, PreparedSourceOutcome.Incomplete);
            }

            if (!complete)
            {
                return Limited(operation, PreparedSourceStoppingLimit.GrantMetadataEntries);
            }

            PreparedSourceTraversal.ListedEntry[] ordered = observed
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToArray();
            if (cursor is not null && cursor.Returned.Any(previous =>
                    !ordered.Contains(previous)))
            {
                return Failure(operation, PreparedSourceOutcome.ChangedInput);
            }

            PreparedSourceTraversal.ListedEntry[] remaining = cursor is null
                ? ordered
                : ordered.Where(entry =>
                    StringComparer.Ordinal.Compare(entry.Name, cursor.LastPath) > 0)
                    .ToArray();
            PreparedSourceTraversal.ListedEntry[] page = remaining
                .Take(request.PageSize)
                .ToArray();
            bool hasMore = remaining.Length > page.Length;
            var entries = new List<PreparedSourceEntry>(page.Length);
            var limitations = new List<PreparedSourceLimitation>();
            foreach (PreparedSourceTraversal.ListedEntry entry in page)
            {
                authorization = await CheckAuthorityAsync(cancellationToken);
                if (authorization is { } stoppedBetweenEntries)
                {
                    return Failure(operation, stoppedBetweenEntries);
                }

                PreparedSourceEntryKind kind = (entry.Attributes & FileAttributes.ReparsePoint) != 0
                    ? PreparedSourceEntryKind.NonTraversableLink
                    : (entry.Attributes & FileAttributes.Directory) != 0
                        ? PreparedSourceEntryKind.Directory
                        : PreparedSourceEntryKind.RegularFile;
                var listed = new PreparedSourceEntry(
                    new PreparedSourceLocation(RepositoryId, entry.Name),
                    kind,
                    kind == PreparedSourceEntryKind.RegularFile ? entry.ByteLength : null);
                entries.Add(listed);
                if (kind == PreparedSourceEntryKind.NonTraversableLink)
                {
                    limitations.Add(new PreparedSourceLimitation(
                        listed.Location,
                        PreparedSourceLimitationReason.NonTraversableLink));
                }
            }

            authorization = await CheckAuthorityAsync(cancellationToken);
            if (authorization is { } stopped)
            {
                return Failure(operation, stopped);
            }

            string? nextToken = null;
            if (hasMore)
            {
                nextToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                PreparedSourceTraversal.ListedEntry[] returned =
                    [.. cursor?.Returned ?? [], .. page];
                _listCursors[nextToken] = new ListCursor(
                    _approval,
                    operation,
                    request.RelativeDirectory,
                    request.PageSize,
                    page[^1].Name,
                    directoryAfter,
                    returned);
            }

            return new PreparedSourceResult
            {
                RepositoryId = RepositoryId,
                Operation = operation,
                Outcome = hasMore
                    ? PreparedSourceOutcome.Limited
                    : entries.Count == 0
                        ? PreparedSourceOutcome.CompleteEmpty
                        : PreparedSourceOutcome.Complete,
                Entries = entries,
                Limitations = limitations,
                Continuation = nextToken,
                StoppingLimit = hasMore ? PreparedSourceStoppingLimit.PageSize : null,
            };
        }
        finally
        {
            _listGate.Release();
        }
    }

    public async Task<PreparedSourceResult> ReadTextRangeAsync(
        PreparedSourceReadTextRangeRequest request,
        CancellationToken cancellationToken = default)
    {
        PreparedSourceOperation operation = PreparedSourceOperation.ReadTextRange;
        if (request is null ||
            !PreparedSourceTraversal.IsCanonicalRelativePath(request.RelativePath, allowEmpty: false))
        {
            return Failure(operation, PreparedSourceOutcome.Denied);
        }

        if (request.ByteOffset < 0 ||
            request.ByteCount is < 1 or > PreparedSourceCallLimits.MaximumReadBytes)
        {
            return Failure(operation, PreparedSourceOutcome.InvalidRange);
        }

        PreparedSourceOutcome? authorization = await CheckAuthorityAsync(cancellationToken);
        if (authorization is { } denied)
        {
            return Failure(operation, denied);
        }

        if (!ReserveOperation())
        {
            return Limited(operation, PreparedSourceStoppingLimit.GrantOperations);
        }

        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? file) =
            PreparedSourceTraversal.OpenRelativeFile(_rootHandle, request.RelativePath);
        if (status != PreparedSourceTraversal.OpenStatus.Opened || file is null)
        {
            return Failure(operation, status switch
            {
                PreparedSourceTraversal.OpenStatus.Denied => PreparedSourceOutcome.Denied,
                PreparedSourceTraversal.OpenStatus.Missing => PreparedSourceOutcome.Missing,
                PreparedSourceTraversal.OpenStatus.Unreadable => PreparedSourceOutcome.Unreadable,
                _ => PreparedSourceOutcome.Incomplete,
            });
        }

        using (file)
        {
            long fileLength;
            try
            {
                fileLength = RandomAccess.GetLength(file);
                if (request.ByteOffset > fileLength)
                {
                    return Failure(operation, PreparedSourceOutcome.InvalidRange);
                }
            }
            catch (IOException)
            {
                return Failure(operation, PreparedSourceOutcome.Unreadable);
            }
            catch (UnauthorizedAccessException)
            {
                return Failure(operation, PreparedSourceOutcome.Unreadable);
            }

            if (request.ByteOffset == fileLength)
            {
                authorization = await CheckAuthorityAsync(cancellationToken);
                return authorization is { } stoppedAtEof
                    ? Failure(operation, stoppedAtEof)
                    : new PreparedSourceResult
                    {
                        RepositoryId = RepositoryId,
                        Operation = operation,
                        Outcome = PreparedSourceOutcome.CompleteEmpty,
                        Text = string.Empty,
                        ReadLocation = new PreparedSourceReadLocation(
                            RepositoryId,
                            request.RelativePath,
                            request.ByteOffset,
                            request.ByteOffset),
                        ObservedFileByteLength = fileLength,
                    };
            }

            int permittedCount;
            PreparedSourceStoppingLimit? stoppingLimit;
            lock (_budgetGate)
            {
                if (_remainingReturnedBytes == 0)
                {
                    return Limited(operation, PreparedSourceStoppingLimit.GrantReturnedBytes);
                }

                if (_remainingExaminedBytes == 0)
                {
                    return Limited(operation, PreparedSourceStoppingLimit.GrantExaminedBytes);
                }

                permittedCount = (int)Math.Min(
                    request.ByteCount,
                    Math.Min(_remainingReturnedBytes, _remainingExaminedBytes));
                stoppingLimit = permittedCount == request.ByteCount
                    ? null
                    : _remainingReturnedBytes <= _remainingExaminedBytes
                        ? PreparedSourceStoppingLimit.GrantReturnedBytes
                        : PreparedSourceStoppingLimit.GrantExaminedBytes;
                _remainingExaminedBytes -= permittedCount;
                _remainingReturnedBytes -= permittedCount;
            }

            byte[] bytes = new byte[permittedCount];
            int count = 0;
            try
            {
                while (count < permittedCount)
                {
                    int read = RandomAccess.Read(
                        file,
                        bytes.AsSpan(count),
                        request.ByteOffset + count);
                    if (read == 0)
                    {
                        break;
                    }

                    count += read;
                }
            }
            catch (IOException)
            {
                RefundReturned(permittedCount);
                return Failure(operation, PreparedSourceOutcome.Unreadable);
            }
            catch (UnauthorizedAccessException)
            {
                RefundReturned(permittedCount);
                return Failure(operation, PreparedSourceOutcome.Unreadable);
            }

            RefundUnused(permittedCount - count);
            if (count < permittedCount && request.ByteOffset + count < fileLength)
            {
                RefundReturned(count);
                return Failure(operation, PreparedSourceOutcome.ChangedInput);
            }

            PreparedSourceText.DecodeResult decoded = PreparedSourceText.DecodeRange(
                file,
                bytes.AsSpan(0, count),
                request.ByteOffset,
                fileLength,
                ReserveExamined);
            if (decoded.Outcome != PreparedSourceOutcome.Complete)
            {
                RefundReturned(count);
                if (decoded.Outcome == PreparedSourceOutcome.Limited)
                {
                    return Limited(operation, PreparedSourceStoppingLimit.GrantExaminedBytes);
                }

                if (decoded.Outcome == PreparedSourceOutcome.InvalidRange &&
                    stoppingLimit is { } cappedBoundary)
                {
                    return Limited(operation, cappedBoundary);
                }

                return Failure(operation, decoded.Outcome);
            }

            try
            {
                if (RandomAccess.GetLength(file) != fileLength)
                {
                    RefundReturned(count);
                    return Failure(operation, PreparedSourceOutcome.ChangedInput);
                }
            }
            catch (IOException)
            {
                RefundReturned(count);
                return Failure(operation, PreparedSourceOutcome.Unreadable);
            }

            authorization = await CheckAuthorityAsync(cancellationToken);
            if (authorization is { } stopped)
            {
                RefundReturned(count);
                return Failure(operation, stopped);
            }

            bool limited = stoppingLimit is not null &&
                request.ByteOffset + count < fileLength;
            return new PreparedSourceResult
            {
                RepositoryId = RepositoryId,
                Operation = operation,
                Outcome = limited
                    ? PreparedSourceOutcome.Limited
                    : PreparedSourceOutcome.Complete,
                Text = decoded.Text,
                ReadLocation = new PreparedSourceReadLocation(
                    RepositoryId,
                    request.RelativePath,
                    request.ByteOffset,
                    request.ByteOffset + count),
                ObservedFileByteLength = fileLength,
                StoppingLimit = limited ? stoppingLimit : null,
            };
        }
    }

    public async Task<PreparedSourceResult> SearchTextAsync(
        PreparedSourceSearchTextRequest request,
        CancellationToken cancellationToken = default)
    {
        PreparedSourceOperation operation = PreparedSourceOperation.SearchText;
        byte[]? literalBytes = request is null
            ? null
            : PreparedSourceText.EncodeLiteral(request.Literal);
        if (request is null || literalBytes is null ||
            !PreparedSourceTraversal.IsCanonicalRelativePath(request.RelativeDirectory, allowEmpty: true) ||
            request.PageSize is < 1 or > PreparedSourceCallLimits.MaximumSearchMatches)
        {
            return Failure(operation, PreparedSourceOutcome.Denied);
        }

        PreparedSourceOutcome? authorization = await CheckAuthorityAsync(cancellationToken);
        if (authorization is { } denied)
        {
            return Failure(operation, denied);
        }

        try
        {
            await _listGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure(operation, PreparedSourceOutcome.Cancelled);
        }

        try
        {
            SearchCursor? cursor = null;
            if (request.Continuation is not null &&
                (!_searchCursors.TryRemove(request.Continuation, out cursor) ||
                 cursor.Approval != _approval ||
                 cursor.RelativeDirectory != request.RelativeDirectory ||
                 cursor.Literal != request.Literal ||
                 cursor.PageSize != request.PageSize))
            {
                return Failure(operation, PreparedSourceOutcome.Denied);
            }

            if (!ReserveOperation())
            {
                return Limited(operation, PreparedSourceStoppingLimit.GrantOperations);
            }
            var run = new SearchRun(request, literalBytes, cursor);
            await ScanDirectoryAsync(request.RelativeDirectory, run, cancellationToken);
            if (run.FailureOutcome is { } failure)
            {
                return run.FailureLocation is { } location
                    ? Failure(operation, failure) with
                    {
                        Limitations =
                        [
                            new PreparedSourceLimitation(
                                new PreparedSourceLocation(RepositoryId, location),
                                PreparedSourceLimitationReason.Unreadable),
                        ],
                    }
                    : Failure(operation, failure);
            }

            if (run.MissingPreviousObservations &&
                run.StoppingLimit is null &&
                run.Matches.Count <= request.PageSize)
            {
                return Failure(operation, PreparedSourceOutcome.ChangedInput);
            }

            if (run.StoppingLimit is { } stoppedBy)
            {
                if (stoppedBy == PreparedSourceStoppingLimit.CallExaminedBytes &&
                    run.ResumePath is not null)
                {
                    authorization = await CheckAuthorityAsync(cancellationToken);
                    if (authorization is { } stoppedByAuthority)
                    {
                        return Failure(operation, stoppedByAuthority);
                    }

                    PreparedSourceMatch[] partial = run.Matches
                        .Take(request.PageSize)
                        .ToArray();
                    PreparedSourceMatchLocation? last = partial.LastOrDefault()?.Location;
                    string workToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                    _searchCursors[workToken] = new SearchCursor(
                        _approval,
                        request.RelativeDirectory,
                        request.Literal,
                        request.PageSize,
                        last?.RelativePath ?? cursor?.LastPath ?? string.Empty,
                        last?.ByteStart ?? cursor?.LastByteStart ?? -1,
                        new Dictionary<string, PreparedSourceTraversal.DirectoryIdentity>(
                            run.ObservedDirectories,
                            StringComparer.Ordinal),
                        new Dictionary<string, PreparedSourceTraversal.FileIdentity>(
                            run.ObservedFiles,
                            StringComparer.Ordinal),
                        run.ResumePath,
                        run.ResumeOffset,
                        run.ResumeScanner);
                    return new PreparedSourceResult
                    {
                        RepositoryId = RepositoryId,
                        Operation = operation,
                        Outcome = PreparedSourceOutcome.Limited,
                        Matches = partial,
                        Continuation = workToken,
                        StoppingLimit = stoppedBy,
                    };
                }

                return Limited(operation, stoppedBy);
            }

            authorization = await CheckAuthorityAsync(cancellationToken);
            if (authorization is { } stopped)
            {
                return Failure(operation, stopped);
            }

            bool hasMore = run.Matches.Count > request.PageSize;
            PreparedSourceMatch[] page = run.Matches.Take(request.PageSize).ToArray();
            string? nextToken = null;
            if (hasMore)
            {
                nextToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                PreparedSourceMatchLocation last = page[^1].Location;
                _searchCursors[nextToken] = new SearchCursor(
                    _approval,
                    request.RelativeDirectory,
                    request.Literal,
                    request.PageSize,
                    last.RelativePath,
                    last.ByteStart,
                    new Dictionary<string, PreparedSourceTraversal.DirectoryIdentity>(
                        run.ObservedDirectories,
                        StringComparer.Ordinal),
                    new Dictionary<string, PreparedSourceTraversal.FileIdentity>(
                        run.ObservedFiles,
                        StringComparer.Ordinal),
                    ResumePath: null,
                    ResumeOffset: 0,
                    ResumeScanner: null);
            }

            return new PreparedSourceResult
            {
                RepositoryId = RepositoryId,
                Operation = operation,
                Outcome = hasMore
                    ? PreparedSourceOutcome.Limited
                    : page.Length == 0
                        ? PreparedSourceOutcome.CompleteEmpty
                        : PreparedSourceOutcome.Complete,
                Matches = page,
                Continuation = nextToken,
                StoppingLimit = hasMore ? PreparedSourceStoppingLimit.PageSize : null,
            };
        }
        finally
        {
            _listGate.Release();
        }
    }

    private async Task ScanDirectoryAsync(
        string relativeDirectory,
        SearchRun run,
        CancellationToken cancellationToken)
    {
        SafeFileHandle? opened = null;
        SafeFileHandle directory = _rootHandle;
        if (relativeDirectory.Length != 0)
        {
            (PreparedSourceTraversal.OpenStatus status, opened) =
                PreparedSourceTraversal.OpenRelativeDirectory(_rootHandle, relativeDirectory);
            if (status != PreparedSourceTraversal.OpenStatus.Opened || opened is null)
            {
                run.FailureOutcome = status switch
                {
                    PreparedSourceTraversal.OpenStatus.Denied => PreparedSourceOutcome.Denied,
                    PreparedSourceTraversal.OpenStatus.Missing => PreparedSourceOutcome.Missing,
                    _ => PreparedSourceOutcome.Incomplete,
                };
                return;
            }

            directory = opened;
        }

        PreparedSourceTraversal.DirectoryIdentity? directoryIdentity =
            PreparedSourceTraversal.ObserveDirectory(directory);
        try
        {
            if (directoryIdentity is null ||
                (run.Cursor?.ObservedDirectories.TryGetValue(
                    relativeDirectory,
                    out PreparedSourceTraversal.DirectoryIdentity? expectedDirectory) == true &&
                 directoryIdentity != expectedDirectory))
            {
                run.FailureOutcome = PreparedSourceOutcome.ChangedInput;
                return;
            }

            run.ObservedDirectories[relativeDirectory] = directoryIdentity;
            run.PendingDirectories.Remove(relativeDirectory);
            if (_remainingMetadataEntries == 0)
            {
                run.StoppingLimit = PreparedSourceStoppingLimit.GrantMetadataEntries;
                return;
            }

            (PreparedSourceTraversal.OpenStatus status,
                IReadOnlyList<PreparedSourceTraversal.ListedEntry> observed,
                bool complete) = PreparedSourceTraversal.ListImmediate(
                    directory,
                    _remainingMetadataEntries);
            _remainingMetadataEntries -= observed.Count;
            if (status != PreparedSourceTraversal.OpenStatus.Opened)
            {
                run.FailureOutcome = PreparedSourceOutcome.Incomplete;
                return;
            }

            if (!complete)
            {
                run.StoppingLimit = PreparedSourceStoppingLimit.GrantMetadataEntries;
                return;
            }

            foreach (PreparedSourceTraversal.ListedEntry entry in observed.OrderBy(
                         item => item.Name +
                             ((item.Attributes & FileAttributes.Directory) != 0 ? "/" : ""),
                         StringComparer.Ordinal))
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    run.FailureOutcome = PreparedSourceOutcome.Incomplete;
                    return;
                }

                string relativePath = relativeDirectory.Length == 0
                    ? entry.Name
                    : relativeDirectory + "/" + entry.Name;
                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    await ScanDirectoryAsync(relativePath, run, cancellationToken);
                }
                else
                {
                    await ScanFileAsync(relativePath, run, cancellationToken);
                }

                if (run.IsStopped)
                {
                    return;
                }
            }
        }
        finally
        {
            if (directoryIdentity is not null &&
                PreparedSourceTraversal.ObserveDirectory(directory) != directoryIdentity &&
                run.FailureOutcome is null)
            {
                run.FailureOutcome = PreparedSourceOutcome.ChangedInput;
            }

            opened?.Dispose();
        }
    }

    private async Task ScanFileAsync(
        string relativePath,
        SearchRun run,
        CancellationToken cancellationToken)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? file) =
            PreparedSourceTraversal.OpenRelativeFile(_rootHandle, relativePath);
        if (status != PreparedSourceTraversal.OpenStatus.Opened || file is null)
        {
            if (status == PreparedSourceTraversal.OpenStatus.Unreadable)
            {
                run.FailUnreadable(relativePath);
            }
            else
            {
                run.FailureOutcome = PreparedSourceOutcome.Incomplete;
            }
            return;
        }

        using (file)
        {
            PreparedSourceTraversal.FileIdentity? fileIdentity =
                PreparedSourceTraversal.ObserveFile(file);
            if (fileIdentity is null ||
                (run.Cursor?.ObservedFiles.TryGetValue(
                    relativePath,
                    out PreparedSourceTraversal.FileIdentity? expectedFile) == true &&
                 fileIdentity != expectedFile))
            {
                run.FailureOutcome = PreparedSourceOutcome.ChangedInput;
                return;
            }

            run.ObservedFiles[relativePath] = fileIdentity;
            run.PendingFiles.Remove(relativePath);
            if (run.Cursor?.ResumePath is { } resumePath &&
                StringComparer.Ordinal.Compare(relativePath, resumePath) < 0)
            {
                return;
            }

            long length;
            try
            {
                length = RandomAccess.GetLength(file);
            }
            catch (IOException)
            {
                run.FailUnreadable(relativePath);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                run.FailUnreadable(relativePath);
                return;
            }

            bool resumingFile = run.Cursor?.ResumePath == relativePath;
            PreparedSourceText.LiteralScanner scanner = resumingFile
                ? run.Cursor!.ResumeScanner!
                : new PreparedSourceText.LiteralScanner(run.LiteralBytes);
            byte[] buffer = new byte[4096];
            long offset = resumingFile ? run.Cursor!.ResumeOffset : 0;
            while (offset < length)
            {
                PreparedSourceOutcome? authority = await CheckAuthorityAsync(cancellationToken);
                if (authority is { } stopped)
                {
                    run.FailureOutcome = stopped;
                    return;
                }

                int permitted = ReserveSearchExamined(
                    (int)Math.Min(buffer.Length, length - offset),
                    run);
                if (permitted == 0)
                {
                    lock (_budgetGate)
                    {
                        run.StoppingLimit = _remainingExaminedBytes == 0
                            ? PreparedSourceStoppingLimit.GrantExaminedBytes
                            : PreparedSourceStoppingLimit.CallExaminedBytes;
                    }

                    if (run.StoppingLimit == PreparedSourceStoppingLimit.CallExaminedBytes)
                    {
                        run.ResumePath = relativePath;
                        run.ResumeOffset = offset;
                        run.ResumeScanner = scanner;
                    }
                    return;
                }

                int count;
                try
                {
                    count = RandomAccess.Read(file, buffer.AsSpan(0, permitted), offset);
                }
                catch (IOException)
                {
                    run.FailUnreadable(relativePath);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    run.FailUnreadable(relativePath);
                    return;
                }

                if (count < permitted)
                {
                    RefundExamined(permitted - count);
                    run.CallExaminedRemaining += permitted - count;
                }

                if (count == 0)
                {
                    run.FailureOutcome = PreparedSourceOutcome.ChangedInput;
                    return;
                }

                bool continued = scanner.Process(buffer.AsSpan(0, count), match =>
                {
                    if (run.Cursor is not null &&
                        (StringComparer.Ordinal.Compare(relativePath, run.Cursor.LastPath) < 0 ||
                         (relativePath == run.Cursor.LastPath &&
                          match.ByteStart <= run.Cursor.LastByteStart)))
                    {
                        return true;
                    }

                    run.Matches.Add(new PreparedSourceMatch(
                        new PreparedSourceMatchLocation(
                            RepositoryId,
                            relativePath,
                            match.ByteStart,
                            run.LiteralBytes.Length,
                            match.Line,
                            match.UnicodeScalarColumn)));
                    return run.Matches.Count <= run.Request.PageSize;
                });
                if (scanner.UnsupportedText)
                {
                    run.FailureOutcome = PreparedSourceOutcome.UnsupportedText;
                    return;
                }

                if (!continued)
                {
                    if (PreparedSourceTraversal.ObserveFile(file) != fileIdentity)
                    {
                        run.FailureOutcome = PreparedSourceOutcome.ChangedInput;
                    }

                    return;
                }

                offset += count;
            }

            if (scanner.HasIncompleteScalar)
            {
                run.FailureOutcome = PreparedSourceOutcome.UnsupportedText;
            }
            else if (PreparedSourceTraversal.ObserveFile(file) != fileIdentity)
            {
                run.FailureOutcome = PreparedSourceOutcome.ChangedInput;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _disposalCancellation.Cancel();
            _listCursors.Clear();
            _searchCursors.Clear();
            _rootHandle.Dispose();
            _disposalCancellation.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async Task<PreparedSourceOutcome?> CheckAuthorityAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return PreparedSourceOutcome.Cancelled;
        }

        int closed = Volatile.Read(ref _closedOutcome);
        if (closed != 0)
        {
            return (PreparedSourceOutcome)closed;
        }

        int stopped = Volatile.Read(ref _stoppedOutcome);
        if (stopped != 0)
        {
            return (PreparedSourceOutcome)stopped;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CloseStopped(PreparedSourceOutcome.Cancelled);
        }

        TimeSpan remaining = _remainingDeadline - Stopwatch.GetElapsedTime(_deadlineStarted);
        if (remaining <= TimeSpan.Zero)
        {
            return CloseStopped(PreparedSourceOutcome.DeadlineExceeded);
        }

        PreparedSourceAuthorityResult current;
        using var deadline = new CancellationTokenSource(remaining);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token,
            _disposalToken);
        try
        {
            current = await _authority.VerifyCurrentAsync(_approval, linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CloseStopped(PreparedSourceOutcome.Cancelled);
        }
        catch (OperationCanceledException) when (_disposalToken.IsCancellationRequested)
        {
            return PreparedSourceOutcome.Cancelled;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return CloseStopped(PreparedSourceOutcome.DeadlineExceeded);
        }
        catch (Exception)
        {
            return CloseAuthority(PreparedSourceOutcome.AuthorityUnavailable);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CloseStopped(PreparedSourceOutcome.Cancelled);
        }

        if (DeadlineElapsed)
        {
            return CloseStopped(PreparedSourceOutcome.DeadlineExceeded);
        }

        if (current is null || current.Status == PreparedSourceAuthorityStatus.Unavailable)
        {
            return CloseAuthority(PreparedSourceOutcome.AuthorityUnavailable);
        }

        if (current.Status != PreparedSourceAuthorityStatus.Current ||
            current.CurrentApproval != _approval)
        {
            return CloseAuthority(PreparedSourceOutcome.Denied);
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            return PreparedSourceOutcome.Cancelled;
        }

        closed = Volatile.Read(ref _closedOutcome);
        if (closed != 0)
        {
            return (PreparedSourceOutcome)closed;
        }

        stopped = Volatile.Read(ref _stoppedOutcome);
        return stopped == 0 ? null : (PreparedSourceOutcome)stopped;
    }

    private PreparedSourceOutcome CloseStopped(PreparedSourceOutcome outcome)
    {
        int existing = Interlocked.CompareExchange(ref _stoppedOutcome, (int)outcome, 0);
        _listCursors.Clear();
        _searchCursors.Clear();
        return existing == 0 ? outcome : (PreparedSourceOutcome)existing;
    }

    private PreparedSourceOutcome CloseAuthority(PreparedSourceOutcome outcome)
    {
        int existing = Interlocked.CompareExchange(ref _closedOutcome, (int)outcome, 0);
        _listCursors.Clear();
        _searchCursors.Clear();
        return existing == 0 ? outcome : (PreparedSourceOutcome)existing;
    }

    private PreparedSourceResult Limited(
        PreparedSourceOperation operation,
        PreparedSourceStoppingLimit limit) =>
        new()
        {
            RepositoryId = RepositoryId,
            Operation = operation,
            Outcome = PreparedSourceOutcome.Limited,
            StoppingLimit = limit,
        };

    private bool ReserveOperation()
    {
        lock (_budgetGate)
        {
            if (_remainingOperations == 0)
            {
                return false;
            }

            _remainingOperations--;
            return true;
        }
    }

    private void RefundUnused(int count)
    {
        if (count == 0)
        {
            return;
        }

        lock (_budgetGate)
        {
            _remainingExaminedBytes += count;
            _remainingReturnedBytes += count;
        }
    }

    private void RefundReturned(int count)
    {
        lock (_budgetGate)
        {
            _remainingReturnedBytes += count;
        }
    }

    private bool ReserveExamined(int count)
    {
        lock (_budgetGate)
        {
            if (_remainingExaminedBytes < count)
            {
                return false;
            }

            _remainingExaminedBytes -= count;
            return true;
        }
    }

    private int ReserveSearchExamined(int requested, SearchRun run)
    {
        lock (_budgetGate)
        {
            int permitted = (int)Math.Min(
                requested,
                Math.Min(_remainingExaminedBytes, run.CallExaminedRemaining));
            _remainingExaminedBytes -= permitted;
            run.CallExaminedRemaining -= permitted;
            return permitted;
        }
    }

    private void RefundExamined(int count)
    {
        lock (_budgetGate)
        {
            _remainingExaminedBytes += count;
        }
    }

    private sealed record ListCursor(
        PreparedSourceApproval Approval,
        PreparedSourceOperation Operation,
        string RelativeDirectory,
        int PageSize,
        string LastPath,
        PreparedSourceTraversal.DirectoryIdentity Directory,
        IReadOnlyList<PreparedSourceTraversal.ListedEntry> Returned);

    private sealed record SearchCursor(
        PreparedSourceApproval Approval,
        string RelativeDirectory,
        string Literal,
        int PageSize,
        string LastPath,
        long LastByteStart,
        IReadOnlyDictionary<string, PreparedSourceTraversal.DirectoryIdentity> ObservedDirectories,
        IReadOnlyDictionary<string, PreparedSourceTraversal.FileIdentity> ObservedFiles,
        string? ResumePath,
        long ResumeOffset,
        PreparedSourceText.LiteralScanner? ResumeScanner);

    private sealed class SearchRun
    {
        internal SearchRun(
            PreparedSourceSearchTextRequest request,
            byte[] literalBytes,
            SearchCursor? cursor)
        {
            Request = request;
            LiteralBytes = literalBytes;
            Cursor = cursor;
            if (cursor is not null)
            {
                foreach (var directory in cursor.ObservedDirectories)
                {
                    ObservedDirectories.Add(directory.Key, directory.Value);
                }

                foreach (var file in cursor.ObservedFiles)
                {
                    ObservedFiles.Add(file.Key, file.Value);
                }
            }

            PendingDirectories = cursor is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(cursor.ObservedDirectories.Keys, StringComparer.Ordinal);
            PendingFiles = cursor is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(cursor.ObservedFiles.Keys, StringComparer.Ordinal);
        }

        internal PreparedSourceSearchTextRequest Request { get; }
        internal byte[] LiteralBytes { get; }
        internal SearchCursor? Cursor { get; }
        internal List<PreparedSourceMatch> Matches { get; } = [];
        internal Dictionary<string, PreparedSourceTraversal.DirectoryIdentity> ObservedDirectories { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, PreparedSourceTraversal.FileIdentity> ObservedFiles { get; } =
            new(StringComparer.Ordinal);
        internal HashSet<string> PendingDirectories { get; }
        internal HashSet<string> PendingFiles { get; }
        internal bool MissingPreviousObservations =>
            PendingDirectories.Count != 0 || PendingFiles.Count != 0;
        internal int CallExaminedRemaining { get; set; } = PreparedSourceCallLimits.MaximumExaminedBytes;
        internal PreparedSourceOutcome? FailureOutcome { get; set; }
        internal string? FailureLocation { get; private set; }
        internal PreparedSourceStoppingLimit? StoppingLimit { get; set; }
        internal string? ResumePath { get; set; }
        internal long ResumeOffset { get; set; }
        internal PreparedSourceText.LiteralScanner? ResumeScanner { get; set; }
        internal bool IsStopped => FailureOutcome is not null ||
            StoppingLimit is not null ||
            Matches.Count > Request.PageSize;

        internal void FailUnreadable(string relativePath)
        {
            FailureOutcome = PreparedSourceOutcome.Incomplete;
            FailureLocation = relativePath;
        }
    }

    private PreparedSourceResult Failure(
        PreparedSourceOperation operation,
        PreparedSourceOutcome outcome) =>
        new()
        {
            RepositoryId = RepositoryId,
            Operation = operation,
            Outcome = outcome,
        };
}
