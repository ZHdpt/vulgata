using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

public enum PreparedToolPurpose
{
    JdtNavigation,
    CodeGraphPreparation,
}

public enum PreparedToolUseAuthorityStatus
{
    Current,
    Denied,
    Unavailable,
}

public sealed record PreparedToolUseAuthorityResult(PreparedToolUseAuthorityStatus Status);

public interface IPreparedToolUseAuthorizer
{
    Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
        PreparedSourceApproval approval,
        PreparedToolPurpose toolPurpose,
        CancellationToken cancellationToken = default);
}

public sealed record PreparedToolWorkspaceGrantRequest(
    PreparedSourceApproval Approval,
    string PhysicalRootPath,
    PreparedToolPurpose ToolPurpose,
    TimeSpan Deadline);

public enum PreparedToolWorkspaceIssueStatus
{
    Issued,
    Denied,
    AuthorityUnavailable,
    RootUnavailable,
    UnsupportedPlatform,
    Cancelled,
    DeadlineExceeded,
}

public sealed record PreparedToolWorkspaceIssueResult(
    PreparedToolWorkspaceIssueStatus Status,
    PreparedToolWorkspaceLease? Lease);

public enum PreparedToolWorkspaceLifetime
{
    Open,
    AuthorityClosed,
    ExpiredOrCancelled,
    Disposed,
}

public enum PreparedToolWorkspaceCheckStatus
{
    Current,
    Denied,
    AuthorityUnavailable,
    RootUnavailable,
    Cancelled,
    DeadlineExceeded,
    Disposed,
}

/// <summary>
/// Holds the prepared root privately for one trusted tool operation. The root is
/// never a Worker-facing argument or a serializable property.
/// </summary>
public sealed class PreparedToolWorkspaceLease : IAsyncDisposable
{
    private readonly PreparedSourceApproval _approval;
    private readonly string _rootPath;
    private readonly SafeFileHandle _rootHandle;
    private readonly PreparedSourceTraversal.DirectoryIdentity _rootIdentity;
    private readonly IPreparedSourceCurrentAuthorityVerifier _sourceAuthority;
    private readonly IPreparedToolUseAuthorizer _toolAuthorization;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly long _startedAt;
    private readonly TimeSpan _remainingDeadline;
    private int _lifetime;
    private PreparedToolWorkspaceCheckStatus _closedStatus;

    internal PreparedToolWorkspaceLease(
        PreparedSourceApproval approval,
        PreparedToolPurpose toolPurpose,
        string rootPath,
        SafeFileHandle rootHandle,
        PreparedSourceTraversal.DirectoryIdentity rootIdentity,
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        TimeSpan remainingDeadline)
    {
        _approval = approval;
        RepositoryId = approval.RepositoryId;
        ToolPurpose = toolPurpose;
        _rootPath = rootPath;
        _rootHandle = rootHandle;
        _rootIdentity = rootIdentity;
        _sourceAuthority = sourceAuthority;
        _toolAuthorization = toolAuthorization;
        _remainingDeadline = remainingDeadline;
        _startedAt = Stopwatch.GetTimestamp();
    }

    public Guid RepositoryId { get; }

    public PreparedToolPurpose ToolPurpose { get; }

    internal bool MatchesApproval(PreparedSourceApproval approval) => _approval == approval;

    public PreparedToolWorkspaceLifetime Lifetime =>
        (PreparedToolWorkspaceLifetime)Volatile.Read(ref _lifetime);

    public async Task<PreparedToolWorkspaceCheckStatus> CheckCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync();
        try
        {
            return await CheckCurrentUnderGateAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<PreparedToolWorkspaceCheckStatus> UseContainedRootAsync(
        PreparedToolPurpose expectedPurpose,
        Func<PreparedToolRootScope, CancellationToken, ValueTask> use,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(use);
        await _gate.WaitAsync();
        try
        {
            if (Lifetime != PreparedToolWorkspaceLifetime.Open)
            {
                return _closedStatus;
            }

            if (expectedPurpose != ToolPurpose)
            {
                return PreparedToolWorkspaceCheckStatus.Denied;
            }

            PreparedToolWorkspaceCheckStatus before =
                await CheckCurrentUnderGateAsync(cancellationToken);
            if (before != PreparedToolWorkspaceCheckStatus.Current)
            {
                return before;
            }

            TimeSpan remaining = _remainingDeadline - Stopwatch.GetElapsedTime(_startedAt);
            using var deadline = new CancellationTokenSource(remaining);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, deadline.Token);
            var scope = new PreparedToolRootScope(
                _rootPath, _rootHandle, _rootIdentity, RepositoryId);
            try
            {
                await use(scope, linked.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }
            catch (Exception)
            {
                return Close(PreparedToolWorkspaceCheckStatus.RootUnavailable);
            }
            finally
            {
                scope.Invalidate();
            }

            return await CheckCurrentUnderGateAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PreparedToolWorkspaceCheckStatus> CheckCurrentUnderGateAsync(
        CancellationToken cancellationToken)
    {
            if (Lifetime != PreparedToolWorkspaceLifetime.Open)
            {
                return _closedStatus;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }

            TimeSpan remaining = _remainingDeadline - Stopwatch.GetElapsedTime(_startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }

            using var deadline = new CancellationTokenSource(remaining);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, deadline.Token);
            try
            {
                PreparedSourceAuthorityResult source = await _sourceAuthority.VerifyCurrentAsync(
                    _approval, linked.Token);
                if (source is null || source.Status == PreparedSourceAuthorityStatus.Unavailable)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.AuthorityUnavailable);
                }

                if (source.Status != PreparedSourceAuthorityStatus.Current ||
                    source.CurrentApproval != _approval)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.Denied);
                }

                PreparedToolUseAuthorityResult tool = await _toolAuthorization.VerifyCurrentAsync(
                    _approval, ToolPurpose, linked.Token);
                if (tool is null || tool.Status == PreparedToolUseAuthorityStatus.Unavailable)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.AuthorityUnavailable);
                }

                if (tool.Status != PreparedToolUseAuthorityStatus.Current)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.Denied);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }
            catch (Exception)
            {
                return Close(PreparedToolWorkspaceCheckStatus.AuthorityUnavailable);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }

            if (deadline.IsCancellationRequested ||
                Stopwatch.GetElapsedTime(_startedAt) >= _remainingDeadline)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }

            (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? observedRoot) =
                PreparedSourceTraversal.OpenRoot(_rootPath);
            if (status != PreparedSourceTraversal.OpenStatus.Opened || observedRoot is null)
            {
                return Close(PreparedToolWorkspaceCheckStatus.RootUnavailable);
            }

            using (observedRoot)
            {
                if (!IsSameRootObject(PreparedSourceTraversal.ObserveDirectory(observedRoot), _rootIdentity) ||
                    !IsSameRootObject(PreparedSourceTraversal.ObserveDirectory(_rootHandle), _rootIdentity))
                {
                    return Close(PreparedToolWorkspaceCheckStatus.RootUnavailable);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }

            if (deadline.IsCancellationRequested ||
                Stopwatch.GetElapsedTime(_startedAt) >= _remainingDeadline)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }

            return PreparedToolWorkspaceCheckStatus.Current;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (Lifetime != PreparedToolWorkspaceLifetime.Disposed)
            {
                _rootHandle.Dispose();
                _closedStatus = PreparedToolWorkspaceCheckStatus.Disposed;
                Volatile.Write(ref _lifetime, (int)PreparedToolWorkspaceLifetime.Disposed);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private PreparedToolWorkspaceCheckStatus Close(PreparedToolWorkspaceCheckStatus status)
    {
        _rootHandle.Dispose();
        _closedStatus = status;
        Volatile.Write(ref _lifetime, (int)(status is PreparedToolWorkspaceCheckStatus.Denied or
            PreparedToolWorkspaceCheckStatus.AuthorityUnavailable
            ? PreparedToolWorkspaceLifetime.AuthorityClosed
            : PreparedToolWorkspaceLifetime.ExpiredOrCancelled));
        return status;
    }

    internal static bool IsSameRootObject(
        PreparedSourceTraversal.DirectoryIdentity? observed,
        PreparedSourceTraversal.DirectoryIdentity expected) =>
        observed is not null &&
        observed.VolumeSerialNumber == expected.VolumeSerialNumber &&
        observed.FileIndexHigh == expected.FileIndexHigh &&
        observed.FileIndexLow == expected.FileIndexLow;
}

internal enum PreparedToolOwnedEntryPathClassification
{
    Unknown,
    Other,
    Owned,
}

/// <summary>
/// A short-lived trusted view of the held prepared root. It is valid only while
/// the lease's serialized callback is running and never leaves Infrastructure.
/// </summary>
internal sealed class PreparedToolRootScope
{
    private const string CodeGraphEntryName = ".codegraph";
    private readonly string _rootPath;
    private readonly SafeFileHandle _rootHandle;
    private readonly PreparedSourceTraversal.DirectoryIdentity _rootIdentity;
    private readonly Guid _repositoryId;
    private bool _active = true;

    internal PreparedToolRootScope(
        string rootPath,
        SafeFileHandle rootHandle,
        PreparedSourceTraversal.DirectoryIdentity rootIdentity,
        Guid repositoryId)
    {
        _rootPath = rootPath;
        _rootHandle = rootHandle;
        _rootIdentity = rootIdentity;
        _repositoryId = repositoryId;
    }

    internal void Invalidate() => _active = false;

    internal string GetPhysicalRootPathForTrustedToolBinding()
    {
        EnsureActive();
        return _rootPath;
    }

    internal PreparedToolDirectoryObjectIdentity? ObserveRootObjectIdentity()
    {
        EnsureActive();
        return ToStableIdentity(PreparedSourceTraversal.ObserveDirectory(_rootHandle));
    }

    internal PreparedToolDirectoryObjectIdentity? ObserveCodeGraphEntryIdentity()
    {
        EnsureActive();
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? entry) =
            PreparedSourceTraversal.OpenRelativeDirectory(_rootHandle, CodeGraphEntryName);
        using (entry)
        {
            return status == PreparedSourceTraversal.OpenStatus.Opened && entry is not null
                ? ToStableIdentity(PreparedSourceTraversal.ObserveDirectory(entry))
                : null;
        }
    }

    internal PreparedToolOwnedEntryPathClassification ClassifyOwnedEntryDescendant(
        string relativePath,
        PreparedToolDirectoryObjectIdentity ownedEntryIdentity)
    {
        EnsureActive();
        if (!PreparedSourceTraversal.IsCanonicalRelativePath(relativePath, allowEmpty: false))
        {
            return PreparedToolOwnedEntryPathClassification.Unknown;
        }

        int separator = relativePath.IndexOf('/');
        if (separator < 0)
        {
            return PreparedToolOwnedEntryPathClassification.Other;
        }

        string firstDirectory = relativePath[..separator];
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? entry) =
            PreparedSourceTraversal.OpenRelativeDirectory(_rootHandle, firstDirectory);
        using (entry)
        {
            PreparedToolDirectoryObjectIdentity? observed =
                status == PreparedSourceTraversal.OpenStatus.Opened && entry is not null
                    ? ToStableIdentity(PreparedSourceTraversal.ObserveDirectory(entry))
                    : null;
            return observed is null
                ? PreparedToolOwnedEntryPathClassification.Unknown
                : observed == ownedEntryIdentity
                    ? PreparedToolOwnedEntryPathClassification.Owned
                    : PreparedToolOwnedEntryPathClassification.Other;
        }
    }

    internal bool IsCodeGraphEntryAbsent()
    {
        EnsureActive();
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? entry) =
            PreparedSourceTraversal.OpenRelativeDirectory(_rootHandle, CodeGraphEntryName);
        entry?.Dispose();
        return status == PreparedSourceTraversal.OpenStatus.Missing;
    }

    internal bool TryCreateCodeGraphEntryExclusively()
    {
        EnsureActive();
        if (!PreparedToolWorkspaceLease.IsSameRootObject(
                PreparedSourceTraversal.ObserveDirectory(_rootHandle), _rootIdentity))
        {
            return false;
        }

        IntPtr nameBuffer = Marshal.StringToHGlobalUni(CodeGraphEntryName);
        IntPtr unicodePointer = IntPtr.Zero;
        try
        {
            var unicode = new UnicodeString
            {
                Length = checked((ushort)(CodeGraphEntryName.Length * sizeof(char))),
                MaximumLength = checked((ushort)((CodeGraphEntryName.Length + 1) * sizeof(char))),
                Buffer = nameBuffer,
            };
            unicodePointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(unicode, unicodePointer, fDeleteOld: false);
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = _rootHandle.DangerousGetHandle(),
                ObjectName = unicodePointer,
                Attributes = 0x00001000, // OBJ_DONT_REPARSE
            };
            bool parentReferenceAdded = false;
            try
            {
                _rootHandle.DangerousAddRef(ref parentReferenceAdded);
                int status = NtCreateFile(
                    out SafeFileHandle entry,
                    0x80 | 0x1 | 0x20 | 0x00100000, // attributes/list/traverse/synchronize
                    ref attributes,
                    out _,
                    IntPtr.Zero,
                    (uint)FileAttributes.Directory,
                    0x00000007, // share read/write/delete
                    2, // FILE_CREATE: never open or adopt an existing entry
                    0x1 | 0x00200000 | 0x20, // directory, open reparse point, synchronous
                    IntPtr.Zero,
                    0);
                using (entry)
                {
                    return status >= 0 && !entry.IsInvalid &&
                        PreparedSourceTraversal.ObserveDirectory(entry) is not null &&
                        PreparedToolWorkspaceLease.IsSameRootObject(
                            PreparedSourceTraversal.ObserveDirectory(_rootHandle), _rootIdentity);
                }
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            finally
            {
                if (parentReferenceAdded)
                {
                    _rootHandle.DangerousRelease();
                }
            }
        }
        finally
        {
            if (unicodePointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(unicodePointer);
            }

            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    internal async Task<bool> RemoveRecordedCodeGraphEntryAsync(
        PreparedToolDirectoryObjectIdentity recordedEntry,
        Func<CancellationToken, Task<bool>> mayRemoveNext,
        CancellationToken cancellationToken)
    {
        EnsureActive();
        if (ObserveCodeGraphEntryIdentity() != recordedEntry)
        {
            return false;
        }

        using SafeFileHandle? entry = OpenChildForDelete(_rootHandle, CodeGraphEntryName);
        if (entry is null || ToStableIdentity(PreparedSourceTraversal.ObserveDirectory(entry)) != recordedEntry)
        {
            return false;
        }

        return await RemoveTreeAsync(entry, mayRemoveNext, 0, cancellationToken);
    }

    private async Task<bool> RemoveTreeAsync(
        SafeFileHandle directory,
        Func<CancellationToken, Task<bool>> mayRemoveNext,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth >= 64)
        {
            return false;
        }

        // Restart enumeration after every removal. Each operation checks authority;
        // no unbounded batch can retain a stale disposal decision.
        for (int removed = 0; removed < 10_000; removed++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await mayRemoveNext(cancellationToken))
            {
                return false;
            }

            (PreparedSourceTraversal.OpenStatus status,
                IReadOnlyList<PreparedSourceTraversal.ListedEntry> entries, bool complete) =
                PreparedSourceTraversal.ListImmediate(directory, 1);
            if (status != PreparedSourceTraversal.OpenStatus.Opened)
            {
                return false;
            }

            if (entries.Count == 0)
            {
                return complete && TryMarkForDeletion(directory);
            }

            using SafeFileHandle? child = OpenChildForDelete(directory, entries[0].Name);
            if (child is null || !TryGetOrdinaryChildType(child, out bool childIsDirectory))
            {
                return false;
            }

            if (childIsDirectory)
            {
                if (!await RemoveTreeAsync(child, mayRemoveNext, depth + 1, cancellationToken))
                {
                    return false;
                }
            }
            else if (!await mayRemoveNext(cancellationToken) || !TryMarkForDeletion(child))
            {
                return false;
            }
        }

        return false;
    }

    private static SafeFileHandle? OpenChildForDelete(SafeFileHandle parent, string name)
    {
        if (!PreparedSourceTraversal.IsCanonicalRelativePath(name, allowEmpty: false) ||
            name.Contains('/'))
        {
            return null;
        }

        IntPtr nameBuffer = Marshal.StringToHGlobalUni(name);
        IntPtr unicodePointer = IntPtr.Zero;
        try
        {
            var unicode = new UnicodeString
            {
                Length = checked((ushort)(name.Length * sizeof(char))),
                MaximumLength = checked((ushort)((name.Length + 1) * sizeof(char))),
                Buffer = nameBuffer,
            };
            unicodePointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(unicode, unicodePointer, fDeleteOld: false);
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(),
                ObjectName = unicodePointer,
                Attributes = 0x00001000, // OBJ_DONT_REPARSE
            };
            bool parentReferenceAdded = false;
            try
            {
                parent.DangerousAddRef(ref parentReferenceAdded);
                int status = NtCreateFile(
                    out SafeFileHandle child,
                    0x00010000 | 0x80 | 0x1 | 0x20 | 0x00100000, // DELETE and inspect/traverse
                    ref attributes,
                    out _,
                    IntPtr.Zero,
                    0,
                    0x00000007,
                    1, // FILE_OPEN, never create a missing child
                    0x00200000 | 0x20, // open reparse point, synchronous
                    IntPtr.Zero,
                    0);
                if (status < 0 || child.IsInvalid)
                {
                    child.Dispose();
                    return null;
                }

                return child;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            finally
            {
                if (parentReferenceAdded)
                {
                    parent.DangerousRelease();
                }
            }
        }
        finally
        {
            if (unicodePointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(unicodePointer);
            }

            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    private static bool TryGetOrdinaryChildType(SafeFileHandle child, out bool isDirectory)
    {
        isDirectory = false;
        if (!GetFileInformationByHandle(child, out FileInformation information) ||
            (information.Attributes & FileAttributes.ReparsePoint) != 0 ||
            information.NumberOfLinks != 1)
        {
            return false;
        }

        isDirectory = (information.Attributes & FileAttributes.Directory) != 0;
        return isDirectory || PreparedSourceTraversal.ObserveFile(child) is not null;
    }

    private static bool TryMarkForDeletion(SafeFileHandle child)
    {
        if (!TryGetOrdinaryChildType(child, out _))
        {
            return false;
        }

        var disposition = new FileDispositionInformation { DeleteFile = true };
        return SetFileInformationByHandle(
            child, 4, ref disposition, (uint)Marshal.SizeOf<FileDispositionInformation>());
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file, out FileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file, int informationClass,
        ref FileDispositionInformation information, uint bufferSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInformation
    {
        [MarshalAs(UnmanagedType.Bool)] public bool DeleteFile;
    }

    // BY_HANDLE_FILE_INFORMATION embeds three FILETIME values aligned to four
    // bytes. Without Pack=4, NumberOfLinks is read from the wrong native field.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct FileInformation
    {
        public FileAttributes Attributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    private static PreparedToolDirectoryObjectIdentity? ToStableIdentity(
        PreparedSourceTraversal.DirectoryIdentity? identity) =>
        identity is null ? null : new PreparedToolDirectoryObjectIdentity(
            identity.VolumeSerialNumber, identity.FileIndexHigh, identity.FileIndexLow);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(
        out SafeFileHandle fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        IntPtr allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        IntPtr eaBuffer,
        uint eaLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public IntPtr Information;
    }

    internal bool IsSeparateDisposableStateRoot(string absoluteStateRoot)
    {
        if (!_active || string.IsNullOrWhiteSpace(absoluteStateRoot) ||
            !Path.IsPathFullyQualified(absoluteStateRoot) ||
            !PreparedToolWorkspaceLease.IsSameRootObject(
                PreparedSourceTraversal.ObserveDirectory(_rootHandle), _rootIdentity))
        {
            return false;
        }

        try
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_rootPath));
            string state = Path.TrimEndingDirectorySeparator(Path.GetFullPath(absoluteStateRoot));
            if (IsWithin(root, state) || IsWithin(state, root))
            {
                return false;
            }

            (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? handle) =
                PreparedSourceTraversal.OpenRoot(state);
            using (handle)
            {
                return status == PreparedSourceTraversal.OpenStatus.Opened &&
                    handle is not null &&
                    PreparedSourceTraversal.ObserveDirectory(handle) is not null;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    internal bool TryMapContainedFileUri(
        string? rawProtocolUri,
        out PreparedSourceLocation? relativeLocation)
    {
        relativeLocation = null;
        if (rawProtocolUri is null || !rawProtocolUri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) ||
            rawProtocolUri.Length <= 11 || !IsAsciiLetter(rawProtocolUri[8]) ||
            rawProtocolUri[9] != ':' || rawProtocolUri[10] != '/' ||
            rawProtocolUri.Contains('\\') || rawProtocolUri.Contains('?') ||
            rawProtocolUri.Contains('#') || rawProtocolUri.AsSpan(11).Contains(':') ||
            !HasOnlyOrdinaryEscapes(rawProtocolUri))
        {
            return false;
        }

        foreach (string segment in rawProtocolUri[11..].Split('/'))
        {
            if (segment is "" or "." or "..")
            {
                return false;
            }
        }

        return Uri.TryCreate(rawProtocolUri, UriKind.Absolute, out Uri? uri) &&
            TryMapContainedFileUri(uri, out relativeLocation);
    }

    internal bool TryMapContainedFileUri(
        Uri rawUri,
        out PreparedSourceLocation? relativeLocation)
    {
        relativeLocation = null;
        if (!_active || rawUri is null || !rawUri.IsAbsoluteUri || !rawUri.IsFile ||
            rawUri.IsUnc || rawUri.Query.Length != 0 || rawUri.Fragment.Length != 0 ||
            !HasOnlyOrdinaryEscapes(rawUri.OriginalString) ||
            !PreparedToolWorkspaceLease.IsSameRootObject(
                PreparedSourceTraversal.ObserveDirectory(_rootHandle), _rootIdentity))
        {
            return false;
        }

        try
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_rootPath));
            string file = Path.GetFullPath(rawUri.LocalPath);
            if (!IsWithin(file, root) || file.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (!PreparedSourceTraversal.IsCanonicalRelativePath(relativePath, allowEmpty: false))
            {
                return false;
            }

            (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? handle) =
                PreparedSourceTraversal.OpenRelativeFile(_rootHandle, relativePath);
            using (handle)
            {
                if (status != PreparedSourceTraversal.OpenStatus.Opened || handle is null ||
                    PreparedSourceTraversal.ObserveFile(handle) is null ||
                    !PreparedToolWorkspaceLease.IsSameRootObject(
                        PreparedSourceTraversal.ObserveDirectory(_rootHandle), _rootIdentity))
                {
                    return false;
                }
            }

            relativeLocation = new PreparedSourceLocation(_repositoryId, relativePath);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsWithin(string path, string root) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsAsciiLetter(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool HasOnlyOrdinaryEscapes(string uri)
    {
        var strictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
        for (int index = 0; index < uri.Length; index++)
        {
            if (uri[index] != '%')
            {
                continue;
            }

            var escapedBytes = new List<byte>();
            while (index < uri.Length && uri[index] == '%')
            {
                if (index > uri.Length - 3 ||
                    !byte.TryParse(
                        uri.AsSpan(index + 1, 2),
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out byte decoded) ||
                    decoded < 0x80 && decoded != 0x20)
                {
                    // Encoded dots, separators, percent signs and other ASCII
                    // syntax must not gain a second path interpretation.
                    return false;
                }

                escapedBytes.Add(decoded);
                index += 3;
            }

            try
            {
                _ = strictUtf8.GetString([.. escapedBytes]);
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            index--;
        }

        return true;
    }

    private void EnsureActive()
    {
        if (!_active)
        {
            throw new InvalidOperationException("Prepared root scope is no longer current.");
        }
    }
}

/// <summary>
/// Issues a trusted, purpose-specific physical workspace lease only after both
/// current source approval and separate tool authorization are verified.
/// </summary>
public sealed class PreparedToolWorkspaceIssuer
{
    private readonly IPreparedSourceCurrentAuthorityVerifier _sourceAuthority;
    private readonly IPreparedToolUseAuthorizer _toolAuthorization;

    public PreparedToolWorkspaceIssuer(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization)
    {
        _sourceAuthority = sourceAuthority ?? throw new ArgumentNullException(nameof(sourceAuthority));
        _toolAuthorization = toolAuthorization ?? throw new ArgumentNullException(nameof(toolAuthorization));
    }

    public async Task<PreparedToolWorkspaceIssueResult> IssueAsync(
        PreparedToolWorkspaceGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.Cancelled);
        }

        if (!ValidRequest(request))
        {
            return Failed(PreparedToolWorkspaceIssueStatus.Denied);
        }

        if (request.Deadline == TimeSpan.Zero)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }

        long startedAt = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(request.Deadline);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token);

        try
        {
            PreparedSourceAuthorityResult source = await _sourceAuthority.VerifyCurrentAsync(
                request.Approval, linked.Token);
            if (source is null || source.Status == PreparedSourceAuthorityStatus.Unavailable)
            {
                return Failed(PreparedToolWorkspaceIssueStatus.AuthorityUnavailable);
            }

            if (source.Status != PreparedSourceAuthorityStatus.Current ||
                source.CurrentApproval != request.Approval)
            {
                return Failed(PreparedToolWorkspaceIssueStatus.Denied);
            }

            PreparedToolUseAuthorityResult tool = await _toolAuthorization.VerifyCurrentAsync(
                request.Approval, request.ToolPurpose, linked.Token);
            if (tool is null || tool.Status == PreparedToolUseAuthorityStatus.Unavailable)
            {
                return Failed(PreparedToolWorkspaceIssueStatus.AuthorityUnavailable);
            }

            if (tool.Status != PreparedToolUseAuthorityStatus.Current)
            {
                return Failed(PreparedToolWorkspaceIssueStatus.Denied);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.Cancelled);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }
        catch (Exception)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.AuthorityUnavailable);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.Cancelled);
        }

        if (deadline.IsCancellationRequested || Stopwatch.GetElapsedTime(startedAt) >= request.Deadline)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }

        if (!OperatingSystem.IsWindows())
        {
            return Failed(PreparedToolWorkspaceIssueStatus.UnsupportedPlatform);
        }

        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? root) =
            PreparedSourceTraversal.OpenRoot(request.PhysicalRootPath);
        if (root is null)
        {
            return Failed(status == PreparedSourceTraversal.OpenStatus.Denied
                ? PreparedToolWorkspaceIssueStatus.Denied
                : PreparedToolWorkspaceIssueStatus.RootUnavailable);
        }

        if (cancellationToken.IsCancellationRequested || deadline.IsCancellationRequested ||
            Stopwatch.GetElapsedTime(startedAt) >= request.Deadline)
        {
            root.Dispose();
            return Failed(cancellationToken.IsCancellationRequested
                ? PreparedToolWorkspaceIssueStatus.Cancelled
                : PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }

        PreparedSourceTraversal.DirectoryIdentity? rootIdentity =
            PreparedSourceTraversal.ObserveDirectory(root);
        if (rootIdentity is null)
        {
            root.Dispose();
            return Failed(PreparedToolWorkspaceIssueStatus.RootUnavailable);
        }

        TimeSpan remaining = request.Deadline - Stopwatch.GetElapsedTime(startedAt);
        if (remaining <= TimeSpan.Zero)
        {
            root.Dispose();
            return Failed(PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }

        return new PreparedToolWorkspaceIssueResult(
            PreparedToolWorkspaceIssueStatus.Issued,
            new PreparedToolWorkspaceLease(
                request.Approval,
                request.ToolPurpose,
                request.PhysicalRootPath,
                root,
                rootIdentity,
                _sourceAuthority,
                _toolAuthorization,
                remaining));
    }

    private static bool ValidRequest(PreparedToolWorkspaceGrantRequest request) =>
        request.Approval is { } approval &&
        approval.BatchId != Guid.Empty &&
        approval.RepositoryId != Guid.Empty &&
        approval.Purpose == PreparedSourcePurpose.WorkerSourceRead &&
        !string.IsNullOrWhiteSpace(approval.SubjectId) &&
        !string.IsNullOrWhiteSpace(approval.ApprovalIdentity) &&
        !string.IsNullOrWhiteSpace(approval.RootBindingIdentity) &&
        !string.IsNullOrWhiteSpace(approval.OperatorLabel) &&
        Enum.IsDefined(request.ToolPurpose) &&
        request.Deadline >= TimeSpan.Zero &&
        request.Deadline <= PreparedSourceGrantLimits.Default.Deadline &&
        !string.IsNullOrWhiteSpace(request.PhysicalRootPath) &&
        Path.IsPathFullyQualified(request.PhysicalRootPath);

    private static PreparedToolWorkspaceIssueResult Failed(PreparedToolWorkspaceIssueStatus status) =>
        new(status, Lease: null);
}
