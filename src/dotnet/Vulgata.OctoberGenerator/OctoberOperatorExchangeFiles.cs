using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Vulgata.OctoberGenerator;

internal sealed class OctoberOperatorCleanupUnconfirmedException(Exception cause)
    : IOException("Operator exchange cleanup was not confirmed.", cause);

/// <summary>Windows-only ownership of one fresh, fixed-name operator exchange.</summary>
internal sealed class OctoberOperatorExchangeFiles : IDisposable
{
    private const uint ReadAttributes = 0x80, Synchronize = 0x100000, Delete = 0x10000;
    private const uint OpenReparsePoint = 0x200000, Synchronous = 0x20, DirectoryFile = 1, NonDirectoryFile = 0x40;
    private readonly List<SafeFileHandle> _ancestors = [];
    private SafeFileHandle? _directory;
    private SafeFileHandle? _request;
    private FileIdentity? _requestIdentity;
    private bool _requestWritable;
    private string _requestName = "root-selection.request.tmp";
    private SafeFileHandle? _response;
    private bool _disposed;

    internal static OctoberOperatorExchangeFiles Create(string path)
    {
        var owner = new OctoberOperatorExchangeFiles();
        try
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new InvalidOperationException("Operator directory must be a local absolute path.");
            string full = Path.GetFullPath(path);
            string volume = Path.GetPathRoot(full)!;
            if (volume.Length != 3 || !char.IsLetter(volume[0]) || volume[1] != ':' || volume[2] != '\\')
                throw new InvalidOperationException("Operator directory requires a local drive.");
            string[] components = full[volume.Length..].Split('\\');
            if (components.Length == 0 || components.Any(component => !ValidComponent(component)))
                throw new InvalidOperationException("Operator directory is invalid.");
            SafeFileHandle current = CreateFileW(volume, ReadAttributes | Synchronize | 0x21, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
            owner._ancestors.Add(current);
            CheckKind(current, directory: true);
            for (int index = 0; index < components.Length - 1; index++)
            {
                current = Open(current, components[index], directory: true, create: false, writable: false)!;
                owner._ancestors.Add(current);
            }
            // FILE_CREATE returns the newly created object itself; there is no
            // exists/create/reopen interval in which a foreign directory can win.
            owner._directory = Open(current, components[^1], directory: true, create: true, writable: true);
            return owner;
        }
        catch { owner.Dispose(); throw; }
    }

    internal void PublishRequest(byte[] bytes)
    {
        _request = Open(_directory!, "root-selection.request.tmp", directory: false, create: true, writable: true);
        _requestWritable = true;
        _requestIdentity = Identity(_request!);
        using (var borrowed = Borrow(_request!))
        using (var stream = new FileStream(borrowed, FileAccess.ReadWrite))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        // Rename the retained exclusive handle; the visible request is complete,
        // and another process cannot replace or overwrite the object afterward.
        Rename(_request!, _directory!, "root-selection.request.json");
        _requestName = "root-selection.request.json";
        _request.Dispose();
        _request = null;
        _requestWritable = false;
        SafeFileHandle pinned = Open(_directory!, _requestName, false, false, false)!;
        if (Identity(pinned) != _requestIdentity)
        {
            pinned.Dispose();
            throw new IOException("Operator request identity changed.");
        }
        _request = pinned;
        using var verificationHandle = Borrow(pinned);
        using var verification = new FileStream(verificationHandle, FileAccess.Read);
        if (verification.Length != bytes.Length) throw new IOException("Operator request contents changed.");
        byte[] retained = new byte[bytes.Length];
        verification.ReadExactly(retained);
        if (!retained.AsSpan().SequenceEqual(bytes)) throw new IOException("Operator request contents changed.");
    }

    internal byte[]? TryReadResponse(int maximumBytes)
    {
        _response ??= Open(_directory!, "root-selection.response.json", directory: false, create: false, writable: true, missingAllowed: true);
        if (_response is null) return null;
        using var borrowed = Borrow(_response);
        using var stream = new FileStream(borrowed, FileAccess.Read);
        if (stream.Length > maximumBytes) throw new InvalidOperationException("Operator response exceeds its bound.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Exception? failure = null;
        try
        {
            // A response arriving just before cancellation still belongs to this
            // fixed exchange. Any other entry remains untouched and prevents deletion.
            if (_directory is not null && _response is null)
                try { _response = Open(_directory, "root-selection.response.json", false, false, true, missingAllowed: true); }
                catch (Exception error) { failure = error; }
            if (_requestIdentity is not null)
            {
                try
                {
                    if (!_requestWritable)
                    {
                        _request?.Dispose(); _request = null;
                        SafeFileHandle deleting = Open(_directory!, _requestName, false, false, true)!;
                        if (Identity(deleting) != _requestIdentity)
                        {
                            deleting.Dispose();
                            throw new IOException("Operator request identity changed before cleanup.");
                        }
                        _request = deleting;
                    }
                    if (_request is not null) MarkDelete(_request);
                }
                catch (Exception error) { failure ??= error; }
                finally { _request?.Dispose(); }
            }
            foreach (SafeFileHandle? file in new[] { _response })
            {
                if (file is null) continue;
                try { MarkDelete(file); } catch (Exception error) { failure ??= error; }
                finally { file.Dispose(); }
            }
            if (_directory is not null)
                try { MarkDelete(_directory); } catch (Exception error) { failure ??= error; }
        }
        finally
        {
            _directory?.Dispose();
            for (int index = _ancestors.Count - 1; index >= 0; index--) _ancestors[index].Dispose();
        }
        if (failure is not null) throw new OctoberOperatorCleanupUnconfirmedException(failure);
    }

    private static SafeFileHandle Borrow(SafeFileHandle handle) => new(handle.DangerousGetHandle(), ownsHandle: false);
    private readonly record struct FileIdentity(uint Volume, uint High, uint Low);
    private static FileIdentity Identity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information)) throw new IOException("Operator file identity is unavailable.");
        return new(information.VolumeSerialNumber, information.FileIndexHigh, information.FileIndexLow);
    }
    private static bool ValidComponent(string value) => value.Length is > 0 and <= 255 && value is not ("." or "..") &&
        value.Trim() == value && !value.EndsWith('.') && !value.Any(character => char.IsControl(character) || ":/\\*?<>|\"".Contains(character));

    private static SafeFileHandle? Open(SafeFileHandle parent, string name, bool directory, bool create, bool writable, bool missingAllowed = false)
    {
        IntPtr text = Marshal.StringToHGlobalUni(name), unicode = IntPtr.Zero;
        try
        {
            var value = new UnicodeString { Length = checked((ushort)(name.Length * 2)), MaximumLength = checked((ushort)((name.Length + 1) * 2)), Buffer = text };
            unicode = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(value, unicode, false);
            var attributes = new ObjectAttributes { Length = Marshal.SizeOf<ObjectAttributes>(), RootDirectory = parent.DangerousGetHandle(), ObjectName = unicode, Attributes = 0x1000 | 0x40 };
            uint access = ReadAttributes | Synchronize | (directory ? 0x21u : 1u) | (writable ? Delete : 0u) | (create && !directory ? 2u : 0u);
            int status = NtCreateFile(out var handle, access, ref attributes, out _, IntPtr.Zero, 0,
                directory ? 3u : 1u, create ? 2u : 1u, OpenReparsePoint | Synchronous | (directory ? DirectoryFile : NonDirectoryFile), IntPtr.Zero, 0);
            if (status < 0 || handle.IsInvalid)
            {
                handle.Dispose();
                if (missingAllowed && status is unchecked((int)0xC0000034) or unchecked((int)0xC000003A)) return null;
                throw new IOException("Operator exchange object could not be opened.", new Win32Exception(unchecked((int)RtlNtStatusToDosError(status))));
            }
            try { CheckKind(handle, directory); return handle; }
            catch { handle.Dispose(); throw; }
        }
        finally { if (unicode != IntPtr.Zero) Marshal.FreeHGlobal(unicode); Marshal.FreeHGlobal(text); }
    }

    private static void CheckKind(SafeFileHandle handle, bool directory)
    {
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var information) ||
            (information.Attributes & FileAttributes.ReparsePoint) != 0 ||
            ((information.Attributes & FileAttributes.Directory) != 0) != directory)
            throw new IOException("Operator exchange reparse or object kind is not allowed.");
    }

    private static void Rename(SafeFileHandle file, SafeFileHandle directory, string name)
    {
        int rootOffset = IntPtr.Size, lengthOffset = rootOffset + IntPtr.Size, nameOffset = lengthOffset + 4;
        int bufferLength = nameOffset + (name.Length + 1) * 2;
        IntPtr buffer = Marshal.AllocHGlobal(bufferLength);
        try
        {
            for (int index = 0; index < bufferLength; index++) Marshal.WriteByte(buffer, index, 0);
            Marshal.WriteIntPtr(buffer, rootOffset, directory.DangerousGetHandle());
            Marshal.WriteInt32(buffer, lengthOffset, name.Length * 2);
            Marshal.Copy(name.ToCharArray(), 0, buffer + nameOffset, name.Length);
            int status = NtSetInformationFile(file, out _, buffer, checked((uint)bufferLength), 10);
            if (status < 0)
                throw new IOException("Operator request publication failed.", new Win32Exception(unchecked((int)RtlNtStatusToDosError(status))));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void MarkDelete(SafeFileHandle handle)
    {
        IntPtr disposition = Marshal.AllocHGlobal(1);
        try
        {
            Marshal.WriteByte(disposition, 1);
            if (!SetFileInformationByHandle(handle, 4, disposition, 1))
                throw new IOException("Operator exchange object could not be removed.", new Win32Exception(Marshal.GetLastWin32Error()));
        }
        finally { Marshal.FreeHGlobal(disposition); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, IntPtr information, uint size);
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint access, ref ObjectAttributes attributes, out IoStatusBlock status, IntPtr allocationSize, uint fileAttributes, uint share, uint disposition, uint options, IntPtr ea, uint eaLength);
    [DllImport("ntdll.dll", ExactSpelling = true)] private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtSetInformationFile(SafeFileHandle handle, out IoStatusBlock status, IntPtr information, uint length, int informationClass);
    [StructLayout(LayoutKind.Sequential)] private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)] private struct ObjectAttributes { public int Length; public IntPtr RootDirectory, ObjectName; public uint Attributes; public IntPtr SecurityDescriptor, SecurityQualityOfService; }
    [StructLayout(LayoutKind.Sequential)] private struct IoStatusBlock { public IntPtr Status, Information; }
    // Native FILETIME pairs are DWORD-aligned, as are the following file identity fields.
    [StructLayout(LayoutKind.Sequential, Pack = 4)] private struct FileInformation
    {
        public FileAttributes Attributes;
        public long CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }
}
