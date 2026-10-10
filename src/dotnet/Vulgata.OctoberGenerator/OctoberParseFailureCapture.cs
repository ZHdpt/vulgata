using Vulgata.Agents.WorkerExecution;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Vulgata.OctoberGenerator;

internal sealed record OctoberParseFailureCaptureOptions(string TargetId, Guid BatchId,
    string SubjectId, string ManagementAuthorityId, string Directory);

/// <summary>One explicitly opted-in, original-run diagnostic destination.</summary>
internal sealed class OctoberParseFailureCapture
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly OctoberParseFailureCaptureOptions _options;
    private readonly string[] _excludedPaths;
    private readonly Lock _sync = new();
    private OctoberGenerateExecution? _execution;
    private Guid _runId;
    private int _attempted;

    internal OctoberParseFailureCapture(OctoberParseFailureCaptureOptions options, IReadOnlyList<string> excludedPaths)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _excludedPaths = excludedPaths?.ToArray() ?? throw new ArgumentNullException(nameof(excludedPaths));
    }

    internal bool TryBind(OctoberGenerateExecution execution, Guid runId)
    {
        lock (_sync)
        {
            if (runId == Guid.Empty || _options.BatchId == Guid.Empty || execution is null ||
                string.IsNullOrWhiteSpace(_options.SubjectId) || string.IsNullOrWhiteSpace(_options.ManagementAuthorityId) ||
                execution.Session.SubjectId != _options.SubjectId ||
                execution.Session.ManagementAuthorityId != _options.ManagementAuthorityId ||
                execution.Authority.TargetId != _options.TargetId || execution.Authority.BatchId != _options.BatchId ||
                execution.Setup.TargetId != _options.TargetId || execution.Setup.BatchId != _options.BatchId) return false;
            if (_execution is not null) return ReferenceEquals(execution, _execution) && runId == _runId;
            _execution = execution;
            _runId = runId;
            return true;
        }
    }

    internal async Task<OctoberWorkerCaptureStatus> TryCaptureAsync(OctoberGenerateExecution execution, Guid runId,
        string text, Func<CancellationToken, Task<bool>> verifyCurrent, CancellationToken cancellationToken = default)
    {
        lock (_sync)
            if (execution is null || _execution is null || !ReferenceEquals(execution, _execution) || runId != _runId ||
                !ReferenceEquals(execution.Session, _execution?.Session)) return OctoberWorkerCaptureStatus.Suppressed;
        if (Interlocked.Exchange(ref _attempted, 1) != 0) return OctoberWorkerCaptureStatus.AlreadyAttempted;
        byte[] bytes;
        try
        {
            if (Utf8.GetByteCount(text) > 262144) return OctoberWorkerCaptureStatus.TooLarge;
            bytes = Utf8.GetBytes(text);
        }
        catch (EncoderFallbackException) { return OctoberWorkerCaptureStatus.Unencodable; }
        catch (Exception) { return OctoberWorkerCaptureStatus.Failed; }
        if (cancellationToken.IsCancellationRequested) return OctoberWorkerCaptureStatus.Suppressed;
        CaptureFiles? files = null;
        OctoberWorkerCaptureStatus status = OctoberWorkerCaptureStatus.Failed;
        try
        {
            if (!SafePath(_options.Directory) || _excludedPaths.Any(path => !SafePath(path, allowRoot: true)) ||
                _excludedPaths.Any(path => Overlaps(_options.Directory, path)))
                return OctoberWorkerCaptureStatus.Failed;
            files = CaptureFiles.Create(_options.Directory, _excludedPaths);
            bool current;
            try { current = await verifyCurrent(cancellationToken); }
            catch (Exception) { current = false; }
            if (!current || cancellationToken.IsCancellationRequested)
                status = OctoberWorkerCaptureStatus.Suppressed;
            else
            {
                files.Write(bytes, cancellationToken);
                status = OctoberWorkerCaptureStatus.Captured;
            }
        }
        catch (OperationCanceledException) { status = OctoberWorkerCaptureStatus.Suppressed; }
        catch (CaptureCleanupException) { status = OctoberWorkerCaptureStatus.CleanupUnconfirmed; }
        catch (Exception) { status = OctoberWorkerCaptureStatus.Failed; }
        finally
        {
            try { files?.Dispose(); }
            catch (Exception) { status = OctoberWorkerCaptureStatus.CleanupUnconfirmed; }
        }
        return status;
    }

    internal static bool SafePath(string? path, bool allowRoot = false)
    {
        if (path is null || path.Length is < 3 or > 4096 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\') return false;
        if (path.Length == 3) return allowRoot;
        return path[3..].Split('\\').All(component => component.Length is > 0 and <= 255 && component is not ("." or "..") &&
            component.Trim() == component && !component.EndsWith('.') &&
            !component.Any(character => char.IsControl(character) || ":/\\*?<>|\"".Contains(character)) &&
            !Reserved(component.Split('.')[0]));
    }

    private static bool Reserved(string name) => name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("PRN", StringComparison.OrdinalIgnoreCase) || name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("NUL", StringComparison.OrdinalIgnoreCase) || name.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) || name.Length == 4 && name[3] is >= '1' and <= '9' &&
        (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase));

    internal static bool Overlaps(string path, string excluded)
    {
        string first = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string second = Path.TrimEndingDirectorySeparator(Path.GetFullPath(excluded));
        string firstPrefix = Path.EndsInDirectorySeparator(first) ? first : first + Path.DirectorySeparatorChar;
        string secondPrefix = Path.EndsInDirectorySeparator(second) ? second : second + Path.DirectorySeparatorChar;
        return first.Equals(second, StringComparison.OrdinalIgnoreCase) || first.StartsWith(secondPrefix, StringComparison.OrdinalIgnoreCase) ||
            second.StartsWith(firstPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CaptureCleanupException : IOException;

    /// <summary>Fresh protected objects opened relative to pinned non-reparse ancestors.</summary>
    private sealed class CaptureFiles : IDisposable
    {
        private const uint ReadAttributes = 0x80, ReadControl = 0x20000, Synchronize = 0x100000, Delete = 0x10000;
        private readonly List<SafeFileHandle> _ancestors = [];
        private readonly List<SafeFileHandle> _excluded = [];
        private SafeFileHandle? _directory, _file;
        private SecurityIdentifier? _principal;
        private IntPtr _security;
        private bool _persist;

        internal static CaptureFiles Create(string path, IReadOnlyList<string> excludedPaths)
        {
            var files = new CaptureFiles();
            try
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                files._principal = identity.User ?? throw new IOException("Capture principal unavailable.");
                var descriptor = new RawSecurityDescriptor($"O:{files._principal.Value}D:P(A;;FA;;;{files._principal.Value})");
                byte[] security = new byte[descriptor.BinaryLength];
                descriptor.GetBinaryForm(security, 0);
                files._security = Marshal.AllocHGlobal(security.Length);
                Marshal.Copy(security, 0, files._security, security.Length);
                foreach (string excluded in excludedPaths)
                {
                    var held = CreateFileW(excluded, ReadAttributes, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                    if (held.IsInvalid)
                    {
                        int error = Marshal.GetLastWin32Error(); held.Dispose();
                        if (error is 2 or 3) continue;
                        throw new IOException("Capture exclusion unavailable.");
                    }
                    files._excluded.Add(held);
                    Information(held);
                    CheckCanonical(held, Path.GetFullPath(excluded));
                }
                SafeFileHandle current = CreateFileW(path[..3], ReadAttributes | Synchronize | 0x21, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                files._ancestors.Add(current);
                CheckKind(current, true);
                string currentPath = path[..3];
                CheckCanonical(current, currentPath);
                files.CheckAncestor(current);
                string[] components = path[3..].Split('\\');
                for (int index = 0; index < components.Length - 1; index++)
                {
                    current = Open(current, components[index], true, false, IntPtr.Zero)!;
                    files._ancestors.Add(current);
                    currentPath = Path.Combine(currentPath, components[index]);
                    CheckCanonical(current, currentPath);
                    files.CheckAncestor(current);
                }
                files._directory = Open(current, components[^1], true, true, files._security);
                CheckCanonical(files._directory!, path);
                files.CheckProtected(files._directory!);
                return files;
            }
            catch
            {
                files.Dispose();
                throw;
            }
        }

        private void CheckAncestor(SafeFileHandle ancestor)
        {
            var info = Information(ancestor);
            if (_excluded.Any(handle => Same(info, Information(handle)))) throw new IOException("Capture excluded boundary.");
            using var git = Open(ancestor, ".git", null, false, IntPtr.Zero, missingAllowed: true);
            if (git is not null) throw new IOException("Capture cannot be inside Git.");
        }

        internal void Write(byte[] bytes, CancellationToken token)
        {
            CheckProtected(_directory!);
            token.ThrowIfCancellationRequested();
            _file = Open(_directory!, "final-proposal.txt", false, true, _security);
            CheckProtected(_file!);
            token.ThrowIfCancellationRequested();
            using (var borrowed = new SafeFileHandle(_file!.DangerousGetHandle(), ownsHandle: false))
            using (var stream = new FileStream(borrowed, FileAccess.ReadWrite))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            _persist = true;
        }

        private void CheckProtected(SafeFileHandle handle)
        {
            if (GetSecurityInfo(handle, 1, 5, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out IntPtr descriptor) != 0 || descriptor == IntPtr.Zero)
                throw new IOException("Capture protection unavailable.");
            try
            {
                uint length = GetSecurityDescriptorLength(descriptor);
                if (length is 0 or > 65536) throw new IOException("Capture protection unavailable.");
                byte[] bytes = new byte[length]; Marshal.Copy(descriptor, bytes, 0, bytes.Length);
                var security = new RawSecurityDescriptor(bytes, 0);
                if (!_principal!.Equals(security.Owner) || (security.ControlFlags & ControlFlags.DiscretionaryAclProtected) == 0 ||
                    security.DiscretionaryAcl is not { Count: > 0 } acl || acl.Cast<GenericAce>().Any(ace =>
                        ace is not CommonAce rule || rule.IsInherited || rule.AceQualifier != AceQualifier.AccessAllowed ||
                        rule.AccessMask == 0 || !rule.SecurityIdentifier.Equals(_principal)))
                    throw new IOException("Capture protection unavailable.");
            }
            finally { LocalFree(descriptor); }
        }

        public void Dispose()
        {
            bool failed = false;
            try
            {
                if (!_persist)
                {
                    if (_file is not null) try { MarkDelete(_file); } catch { failed = true; }
                    _file?.Dispose(); _file = null;
                    if (_directory is not null) try { MarkDelete(_directory); } catch { failed = true; }
                }
            }
            finally
            {
                _file?.Dispose(); _file = null;
                _directory?.Dispose(); _directory = null;
                for (int index = _ancestors.Count - 1; index >= 0; index--) _ancestors[index].Dispose();
                foreach (var handle in _excluded) handle.Dispose();
                if (_security != IntPtr.Zero) { Marshal.FreeHGlobal(_security); _security = IntPtr.Zero; }
            }
            if (failed) throw new CaptureCleanupException();
        }

        private static SafeFileHandle? Open(SafeFileHandle parent, string name, bool? directory, bool create, IntPtr security, bool missingAllowed = false)
        {
            IntPtr text = Marshal.StringToHGlobalUni(name), unicode = IntPtr.Zero;
            try
            {
                var value = new UnicodeString { Length = checked((ushort)(name.Length * 2)), MaximumLength = checked((ushort)((name.Length + 1) * 2)), Buffer = text };
                unicode = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>()); Marshal.StructureToPtr(value, unicode, false);
                var attributes = new ObjectAttributes { Length = Marshal.SizeOf<ObjectAttributes>(), RootDirectory = parent.DangerousGetHandle(),
                    ObjectName = unicode, Attributes = 0x1040, SecurityDescriptor = security };
                uint access = ReadAttributes | Synchronize | (directory == true ? 0x21u : 1u) | (create ? Delete | ReadControl : 0u) | (create && directory == false ? 2u : 0u);
                int status = NtCreateFile(out var handle, access, ref attributes, out _, IntPtr.Zero, 0, directory == true ? 3u : 1u,
                    create ? 2u : 1u, 0x200000 | 0x20 | (directory == true ? 1u : directory == false ? 0x40u : 0u), IntPtr.Zero, 0);
                if (status < 0 || handle.IsInvalid)
                {
                    handle.Dispose();
                    if (missingAllowed && status is unchecked((int)0xC0000034) or unchecked((int)0xC000003A)) return null;
                    throw new IOException("Capture object unavailable.");
                }
                try { CheckKind(handle, directory); return handle; }
                catch
                {
                    bool cleanupFailed = false;
                    if (create) try { MarkDelete(handle); } catch { cleanupFailed = true; }
                    handle.Dispose();
                    if (cleanupFailed) throw new CaptureCleanupException();
                    throw;
                }
            }
            finally { if (unicode != IntPtr.Zero) Marshal.FreeHGlobal(unicode); Marshal.FreeHGlobal(text); }
        }

        private static FileInformation Information(SafeFileHandle handle)
        {
            if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info) || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Capture object identity unavailable.");
            return info;
        }
        private static bool Same(FileInformation first, FileInformation second) => first.Volume == second.Volume && first.IndexHigh == second.IndexHigh && first.IndexLow == second.IndexLow;
        private static void CheckCanonical(SafeFileHandle handle, string expected)
        {
            var path = new StringBuilder(8192);
            uint length = GetFinalPathNameByHandleW(handle, path, checked((uint)path.Capacity), 0);
            if (length == 0 || length >= path.Capacity || !path.ToString().Equals(
                "\\\\?\\" + Path.TrimEndingDirectorySeparator(expected), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Capture path mapping is ambiguous.");
        }
        private static void CheckKind(SafeFileHandle handle, bool? directory)
        {
            var info = Information(handle);
            if (directory is { } expected && ((info.Attributes & FileAttributes.Directory) != 0) != expected)
                throw new IOException("Capture object kind unavailable.");
        }
        private static void MarkDelete(SafeFileHandle handle)
        {
            IntPtr value = Marshal.AllocHGlobal(1);
            try { Marshal.WriteByte(value, 1); if (!SetFileInformationByHandle(handle, 4, value, 1)) throw new IOException("Capture cleanup unavailable."); }
            finally { Marshal.FreeHGlobal(value); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint length, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, IntPtr information, uint size);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("advapi32.dll")] private static extern uint GetSecurityInfo(SafeFileHandle handle, uint objectType, uint securityInformation, IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl, out IntPtr descriptor);
        [DllImport("advapi32.dll")] private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);
        [DllImport("ntdll.dll", ExactSpelling = true)] private static extern int NtCreateFile(out SafeFileHandle handle, uint access, ref ObjectAttributes attributes, out IoStatusBlock status, IntPtr allocationSize, uint fileAttributes, uint share, uint disposition, uint options, IntPtr ea, uint eaLength);
        [StructLayout(LayoutKind.Sequential)] private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
        [StructLayout(LayoutKind.Sequential)] private struct ObjectAttributes { public int Length; public IntPtr RootDirectory, ObjectName; public uint Attributes; public IntPtr SecurityDescriptor, SecurityQualityOfService; }
        [StructLayout(LayoutKind.Sequential)] private struct IoStatusBlock { public IntPtr Status, Information; }
        [StructLayout(LayoutKind.Sequential, Pack = 4)] private struct FileInformation
        {
            public FileAttributes Attributes; public long Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
    }
}
