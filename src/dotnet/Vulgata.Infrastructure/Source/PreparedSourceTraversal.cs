using System.Runtime.InteropServices;
using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Vulgata.Infrastructure.Source;

/// <summary>
/// Windows-only, handle-relative opens. Each name is opened from its held parent
/// without following a reparse point; no later path reopen supplies source bytes.
/// </summary>
internal static class PreparedSourceTraversal
{
    private const uint ShareReadWriteDelete = 0x00000007;
    private const uint OpenExisting = 3;
    private const uint FileOpen = 1;
    private const uint FileCreate = 2;
    private const uint FileReadAttributes = 0x80;
    private const uint FileListDirectory = 0x1;
    private const uint FileTraverse = 0x20;
    private const uint Synchronize = 0x00100000;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint SynchronousIoNonAlert = 0x20;
    private const uint BackupSemantics = 0x02000000;
    private const uint ObjectDontReparse = 0x00001000;

    internal enum OpenStatus
    {
        Opened,
        Denied,
        Missing,
        Unreadable,
        Unavailable,
    }

    internal sealed record ListedEntry(
        string Name,
        FileAttributes Attributes,
        long ByteLength,
        long LastWriteTime);

    internal sealed record DirectoryIdentity(
        uint VolumeSerialNumber,
        uint FileIndexHigh,
        uint FileIndexLow,
        long LastWriteTime);

    internal sealed record FileIdentity(
        uint VolumeSerialNumber,
        uint FileIndexHigh,
        uint FileIndexLow,
        long LastWriteTime,
        long ByteLength);

    internal static FileIdentity? ObserveFile(SafeFileHandle file) =>
        GetFileInformationByHandle(file, out FileInformation information) &&
        (information.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0
            ? new FileIdentity(
                information.VolumeSerialNumber,
                information.FileIndexHigh,
                information.FileIndexLow,
                information.LastWriteTime,
                ((long)information.FileSizeHigh << 32) | information.FileSizeLow)
            : null;

    internal static DirectoryIdentity? ObserveDirectory(SafeFileHandle directory) =>
        GetFileInformationByHandle(directory, out FileInformation information) &&
        (information.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) ==
            FileAttributes.Directory
            ? new DirectoryIdentity(
                information.VolumeSerialNumber,
                information.FileIndexHigh,
                information.FileIndexLow,
                information.LastWriteTime)
            : null;

    internal static (OpenStatus Status, IReadOnlyList<ListedEntry> Entries, bool Complete)
        ListImmediate(SafeFileHandle directory, int maximumEntries,
            DirectoryIdentity? excludedOwnedDirectory = null)
    {
        const int bufferLength = 64 * 1024;
        const int fullDirectoryInfo = 0x0e;
        const int fullDirectoryRestartInfo = 0x0f;
        const int noMoreFiles = 18;
        byte[] buffer = new byte[bufferLength];
        var entries = new List<ListedEntry>();
        bool restart = true;

        while (true)
        {
            if (!GetFileInformationByHandleEx(
                    directory,
                    restart ? fullDirectoryRestartInfo : fullDirectoryInfo,
                    buffer,
                    (uint)buffer.Length))
            {
                return Marshal.GetLastWin32Error() == noMoreFiles
                    ? (OpenStatus.Opened, entries, true)
                    : (OpenStatus.Unavailable, entries, false);
            }

            restart = false;
            int offset = 0;
            while (true)
            {
                // FILE_FULL_DIR_INFO has a 68-byte fixed prefix before FileName.
                if (offset > buffer.Length - 68)
                {
                    return (OpenStatus.Unavailable, entries, false);
                }

                ReadOnlySpan<byte> item = buffer.AsSpan(offset);
                uint next = BinaryPrimitives.ReadUInt32LittleEndian(item);
                long lastWriteTime = BinaryPrimitives.ReadInt64LittleEndian(item[24..]);
                long length = BinaryPrimitives.ReadInt64LittleEndian(item[40..]);
                FileAttributes attributes = (FileAttributes)BinaryPrimitives.ReadUInt32LittleEndian(item[56..]);
                uint nameLength = BinaryPrimitives.ReadUInt32LittleEndian(item[60..]);
                if ((nameLength & 1) != 0 || nameLength > item.Length - 68)
                {
                    return (OpenStatus.Unavailable, entries, false);
                }

                string name = Encoding.Unicode.GetString(item.Slice(68, (int)nameLength));
                if (name is not "." and not "..")
                {
                    if (!IsCanonicalRelativePath(name, allowEmpty: false))
                    {
                        return (OpenStatus.Unavailable, entries, false);
                    }

                    bool excluded = false;
                    if (excludedOwnedDirectory is not null &&
                        name.Equals(".codegraph", StringComparison.OrdinalIgnoreCase))
                    {
                        (OpenStatus stateStatus, SafeFileHandle? state) = OpenRelativeDirectory(directory, name);
                        using (state)
                        {
                            DirectoryIdentity? identity = state is null ? null : ObserveDirectory(state);
                            if (stateStatus != OpenStatus.Opened || identity is null ||
                                identity.VolumeSerialNumber != excludedOwnedDirectory.VolumeSerialNumber ||
                                identity.FileIndexHigh != excludedOwnedDirectory.FileIndexHigh ||
                                identity.FileIndexLow != excludedOwnedDirectory.FileIndexLow)
                            {
                                return (OpenStatus.Unavailable, entries, false);
                            }
                            excluded = true;
                        }
                    }
                    if (!excluded && entries.Count >= maximumEntries)
                    {
                        return (OpenStatus.Opened, entries, false);
                    }
                    if (!excluded)
                    {
                        entries.Add(new ListedEntry(name, attributes, length, lastWriteTime));
                    }
                }

                if (next == 0)
                {
                    break;
                }

                if (next < 68 || next > item.Length - 68)
                {
                    return (OpenStatus.Unavailable, entries, false);
                }

                offset += (int)next;
            }
        }

    }

    internal static (OpenStatus Status, SafeFileHandle? Handle) OpenRoot(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return (OpenStatus.Denied, null);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return (OpenStatus.Denied, null);
        }

        string? volumeRoot = Path.GetPathRoot(fullPath);
        if (volumeRoot is null || volumeRoot.Length != 3 ||
            !char.IsLetter(volumeRoot[0]) || volumeRoot[1] != ':' || volumeRoot[2] != '\\')
        {
            // UNC and device paths need their own qualified handle-relative adapter.
            return (OpenStatus.Denied, null);
        }

        SafeFileHandle current = CreateFileW(
            volumeRoot,
            FileReadAttributes | FileListDirectory | FileTraverse | Synchronize,
            ShareReadWriteDelete,
            IntPtr.Zero,
            OpenExisting,
            OpenReparsePoint | BackupSemantics,
            IntPtr.Zero);
        if (current.IsInvalid || !IsOrdinaryDirectory(current))
        {
            current.Dispose();
            return (OpenStatus.Unavailable, null);
        }

        string relativeRoot = fullPath[volumeRoot.Length..];
        if (relativeRoot.Length == 0)
        {
            return (OpenStatus.Opened, current);
        }

        foreach (string component in relativeRoot.Split('\\'))
        {
            if (!IsCanonicalComponent(component))
            {
                current.Dispose();
                return (OpenStatus.Denied, null);
            }

            (OpenStatus status, SafeFileHandle? next) = OpenChild(current, component);
            current.Dispose();
            if (status != OpenStatus.Opened || next is null)
            {
                return (status, null);
            }

            current = next;
            if (!IsOrdinaryDirectory(current))
            {
                OpenStatus rejected = IsReparsePoint(current)
                    ? OpenStatus.Denied
                    : OpenStatus.Unavailable;
                current.Dispose();
                return (rejected, null);
            }
        }

        return (OpenStatus.Opened, current);
    }

    internal static (OpenStatus Status, SafeFileHandle? Handle) OpenRelativeFile(
        SafeFileHandle root,
        string? relativePath,
        DirectoryIdentity? excludedDirectory = null) =>
        OpenRelative(root, relativePath, requireDirectory: false, excludedDirectory);

    internal static (OpenStatus Status, SafeFileHandle? Handle) OpenRelativeDirectory(
        SafeFileHandle root,
        string? relativePath,
        DirectoryIdentity? excludedDirectory = null) =>
        OpenRelative(root, relativePath, requireDirectory: true, excludedDirectory);

    internal static (OpenStatus Status, SafeFileHandle? Handle) CreateRelativeDirectory(
        SafeFileHandle root, string name)
        => !IsCanonicalRelativePath(name, allowEmpty: false) || name.Contains('/')
            ? (OpenStatus.Denied, null)
            : OpenChild(root, name, createDirectory: true);

    private static (OpenStatus Status, SafeFileHandle? Handle) OpenRelative(
        SafeFileHandle root,
        string? relativePath,
        bool requireDirectory,
        DirectoryIdentity? excludedDirectory)
    {
        if (!IsCanonicalRelativePath(relativePath, allowEmpty: false))
        {
            return (OpenStatus.Denied, null);
        }

        string[] components = relativePath!.Split('/');
        SafeFileHandle current = root;
        bool ownsCurrent = false;
        try
        {
            for (int index = 0; index < components.Length; index++)
            {
                (OpenStatus status, SafeFileHandle? next) = OpenChild(current, components[index]);
                if (status != OpenStatus.Opened || next is null)
                {
                    return (status, null);
                }

                if (ownsCurrent)
                {
                    current.Dispose();
                }

                current = next;
                ownsCurrent = true;
                if (IsReparsePoint(current))
                {
                    return (OpenStatus.Denied, null);
                }

                DirectoryIdentity? directoryIdentity = ObserveDirectory(current);
                if (directoryIdentity is not null && excludedDirectory is not null &&
                    directoryIdentity.VolumeSerialNumber == excludedDirectory.VolumeSerialNumber &&
                    directoryIdentity.FileIndexHigh == excludedDirectory.FileIndexHigh &&
                    directoryIdentity.FileIndexLow == excludedDirectory.FileIndexLow)
                {
                    return (OpenStatus.Denied, null);
                }

                bool final = index == components.Length - 1;
                if (!final && !IsOrdinaryDirectory(current))
                {
                    return (OpenStatus.Denied, null);
                }

                if (final && IsOrdinaryDirectory(current) != requireDirectory)
                {
                    return (OpenStatus.Denied, null);
                }
            }

            ownsCurrent = false;
            return (OpenStatus.Opened, current);
        }
        finally
        {
            if (ownsCurrent)
            {
                current.Dispose();
            }
        }
    }

    internal static bool IsCanonicalRelativePath(string? path, bool allowEmpty)
    {
        if (path is null || (path.Length == 0 && !allowEmpty) ||
            path.StartsWith('/') || path.EndsWith('/') ||
            path.Contains('\\') || path.Contains(':'))
        {
            return false;
        }

        return path.Length == 0 || path.Split('/').All(IsCanonicalComponent);
    }

    private static bool IsCanonicalComponent(string component)
    {
        if (component.Length is 0 or > 255 || component is "." or ".." ||
            component.EndsWith('.') || component.EndsWith(' ') ||
            component.Any(character => character < ' ' ||
                character is '<' or '>' or ':' or '"' or '\\' or '/' or '|' or '?' or '*'))
        {
            return false;
        }

        string baseName = component.Split('.')[0];
        if (baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (baseName.Length == 4 &&
            (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
             baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            baseName[3] is (>= '1' and <= '9') or '\u00B9' or '\u00B2' or '\u00B3')
        {
            return false;
        }

        return true;
    }

    private static (OpenStatus Status, SafeFileHandle? Handle) OpenChild(
        SafeFileHandle parent,
        string name,
        bool createDirectory = false)
    {
        IntPtr nameBuffer = Marshal.StringToHGlobalUni(name);
        IntPtr unicodeStringPointer = IntPtr.Zero;
        try
        {
            var unicodeString = new UnicodeString
            {
                Length = checked((ushort)(name.Length * sizeof(char))),
                MaximumLength = checked((ushort)((name.Length + 1) * sizeof(char))),
                Buffer = nameBuffer,
            };
            unicodeStringPointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(unicodeString, unicodeStringPointer, fDeleteOld: false);
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(),
                ObjectName = unicodeStringPointer,
                Attributes = ObjectDontReparse,
            };

            int status;
            SafeFileHandle handle;
            bool parentReferenceAdded = false;
            try
            {
                parent.DangerousAddRef(ref parentReferenceAdded);
                status = NtCreateFile(
                    out handle,
                    FileReadAttributes | FileListDirectory | FileTraverse | Synchronize,
                    ref attributes,
                    out _,
                    IntPtr.Zero,
                    0,
                    ShareReadWriteDelete,
                    createDirectory ? FileCreate : FileOpen,
                    OpenReparsePoint | SynchronousIoNonAlert | (createDirectory ? 1u : 0u),
                    IntPtr.Zero,
                    0);
            }
            catch (ObjectDisposedException)
            {
                return (OpenStatus.Unavailable, null);
            }
            finally
            {
                if (parentReferenceAdded)
                {
                    parent.DangerousRelease();
                }
            }
            if (status < 0 || handle.IsInvalid)
            {
                handle?.Dispose();
                return (status switch
                {
                    unchecked((int)0xC0000034) or unchecked((int)0xC000003A) => OpenStatus.Missing,
                    unchecked((int)0xC0000022) or unchecked((int)0xC0000043) => OpenStatus.Unreadable,
                    unchecked((int)0xC000050B) => OpenStatus.Denied,
                    _ => OpenStatus.Unavailable,
                }, null);
            }

            if (IsReparsePoint(handle))
            {
                handle.Dispose();
                return (OpenStatus.Denied, null);
            }

            return (OpenStatus.Opened, handle);
        }
        finally
        {
            if (unicodeStringPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(unicodeStringPointer);
            }

            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    private static bool IsOrdinaryDirectory(SafeFileHandle handle) =>
        GetFileInformationByHandle(handle, out FileInformation information) &&
        (information.Attributes & FileAttributes.Directory) != 0 &&
        (information.Attributes & FileAttributes.ReparsePoint) == 0;

    private static bool IsReparsePoint(SafeFileHandle handle) =>
        !GetFileInformationByHandle(handle, out FileInformation information) ||
        (information.Attributes & FileAttributes.ReparsePoint) != 0;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out FileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        [Out] byte[] fileInformation,
        uint bufferSize);

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

    // BY_HANDLE_FILE_INFORMATION uses DWORD-aligned FILETIME pairs, not
    // eight-byte aligned managed Int64 fields. Its native size is 52 bytes.
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
}
