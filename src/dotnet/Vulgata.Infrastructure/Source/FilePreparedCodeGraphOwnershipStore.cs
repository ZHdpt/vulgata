using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Vulgata.Infrastructure.Source;

internal enum PreparedCodeGraphStoreProvisionStatus
{
    Provisioned,
    Conflict,
    Unavailable,
}

/// <summary>
/// Stores CodeGraph ownership metadata in a fixed SourceStorage child. Lifecycle
/// transitions remain unavailable until their file compare-and-set is implemented.
/// </summary>
internal sealed class FilePreparedCodeGraphOwnershipStore : IPreparedCodeGraphOwnershipStore
{
    private const string MetadataDirectoryName = ".prepared-codegraph-ownership";
    private const string LedgerFileName = "ledger.json";
    private static readonly byte[] EmptyLedger = Encoding.UTF8.GetBytes(
        "{\"format\":\"vulgata-codegraph-ownership-v1\",\"records\":[]}");

    private readonly RetainedSourceCapturePolicy _sourceStoragePolicy;

    internal FilePreparedCodeGraphOwnershipStore(RetainedSourceCapturePolicy sourceStoragePolicy)
    {
        _sourceStoragePolicy = sourceStoragePolicy ??
            throw new ArgumentNullException(nameof(sourceStoragePolicy));
    }

    internal static async Task<PreparedCodeGraphStoreProvisionStatus> ProvisionNewAsync(
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceStoragePolicy);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? root) =
                PreparedSourceTraversal.OpenRoot(sourceStoragePolicy.RootPath);
            using (root)
            {
                PreparedToolDirectoryObjectIdentity? rootIdentity = Stable(root);
                if (rootStatus != PreparedSourceTraversal.OpenStatus.Opened || rootIdentity is null)
                {
                    return PreparedCodeGraphStoreProvisionStatus.Unavailable;
                }

                (int createStatus, SafeFileHandle? metadata) = OpenRelative(
                    root!, MetadataDirectoryName, directory: true, create: true);
                using (metadata)
                {
                    if (createStatus == unchecked((int)0xC0000035)) // STATUS_OBJECT_NAME_COLLISION
                    {
                        return PreparedCodeGraphStoreProvisionStatus.Conflict;
                    }

                    PreparedToolDirectoryObjectIdentity? metadataIdentity = Stable(metadata);
                    if (createStatus < 0 || metadataIdentity is null || Stable(root) != rootIdentity)
                    {
                        return PreparedCodeGraphStoreProvisionStatus.Unavailable;
                    }

                    (int ledgerStatus, SafeFileHandle? ledger) = OpenRelative(
                        metadata!, LedgerFileName, directory: false, create: true);
                    if (ledgerStatus < 0 || ledger is null || ledger.IsInvalid ||
                        PreparedSourceTraversal.ObserveFile(ledger) is null)
                    {
                        ledger?.Dispose();
                        return PreparedCodeGraphStoreProvisionStatus.Unavailable;
                    }

                    await using (var stream = new FileStream(
                        ledger, FileAccess.ReadWrite, bufferSize: 4096, isAsync: false))
                    {
                        await stream.WriteAsync(EmptyLedger, cancellationToken);
                        await stream.FlushAsync(cancellationToken);
                        stream.Flush(flushToDisk: true);
                        stream.Position = 0;
                        byte[] readback = new byte[EmptyLedger.Length];
                        await stream.ReadExactlyAsync(readback, cancellationToken);
                        if (stream.Length != EmptyLedger.Length ||
                            !readback.AsSpan().SequenceEqual(EmptyLedger))
                        {
                            return PreparedCodeGraphStoreProvisionStatus.Unavailable;
                        }
                    }

                    return Stable(root) == rootIdentity && Stable(metadata) == metadataIdentity
                        ? PreparedCodeGraphStoreProvisionStatus.Provisioned
                        : PreparedCodeGraphStoreProvisionStatus.Unavailable;
                }
            }
        }
        catch (Exception)
        {
            // Any partially initialized child is left unavailable, never adopted.
            return PreparedCodeGraphStoreProvisionStatus.Unavailable;
        }
    }

    public async Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedBinding);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expectedBinding.Phase != PreparedCodeGraphOwnershipPhase.Absent ||
                expectedBinding.Version != 0 || expectedBinding.StateEntryObjectIdentity is not null ||
                !SeparateRoots(_sourceStoragePolicy.RootPath, expectedBinding.PhysicalRootPath))
            {
                return UnavailableLoad();
            }

            (PreparedSourceTraversal.OpenStatus workspaceStatus, SafeFileHandle? workspace) =
                PreparedSourceTraversal.OpenRoot(expectedBinding.PhysicalRootPath);
            using (workspace)
            {
                if (workspaceStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                    Stable(workspace) != expectedBinding.RootObjectIdentity)
                {
                    return UnavailableLoad();
                }
            }

            (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? root) =
                PreparedSourceTraversal.OpenRoot(_sourceStoragePolicy.RootPath);
            using (root)
            {
                PreparedToolDirectoryObjectIdentity? rootIdentity = Stable(root);
                if (rootStatus != PreparedSourceTraversal.OpenStatus.Opened || rootIdentity is null)
                {
                    return UnavailableLoad();
                }

                (PreparedSourceTraversal.OpenStatus metadataStatus, SafeFileHandle? metadata) =
                    PreparedSourceTraversal.OpenRelativeDirectory(root!, MetadataDirectoryName);
                using (metadata)
                {
                    PreparedToolDirectoryObjectIdentity? metadataIdentity = Stable(metadata);
                    if (metadataStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                        metadataIdentity is null)
                    {
                        return UnavailableLoad();
                    }

                    (int ledgerStatus, SafeFileHandle? ledger) = OpenRelative(
                        metadata!, LedgerFileName, directory: false, create: false);
                    if (ledgerStatus < 0 || ledger is null || ledger.IsInvalid ||
                        PreparedSourceTraversal.ObserveFile(ledger) is null)
                    {
                        ledger?.Dispose();
                        return UnavailableLoad();
                    }

                    await using (var stream = new FileStream(
                        ledger, FileAccess.Read, bufferSize: 4096, isAsync: false))
                    {
                        if (stream.Length != EmptyLedger.Length)
                        {
                            return UnavailableLoad();
                        }

                        byte[] bytes = new byte[EmptyLedger.Length];
                        await stream.ReadExactlyAsync(bytes, cancellationToken);
                        if (!bytes.AsSpan().SequenceEqual(EmptyLedger))
                        {
                            return UnavailableLoad();
                        }
                    }

                    return Stable(root) == rootIdentity && Stable(metadata) == metadataIdentity
                        ? new PreparedCodeGraphOwnershipLoadResult(
                            PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, null)
                        : UnavailableLoad();
                }
            }
        }
        catch (Exception)
        {
            return UnavailableLoad();
        }
    }

    public Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
        PreparedCodeGraphWorkspaceOwnership absent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(absent);
        return UnavailableTransition();
    }

    public Task<PreparedCodeGraphOwnershipTransitionResult> CommitCreatedAsync(
        PreparedCodeGraphWorkspaceOwnership creating,
        PreparedToolDirectoryObjectIdentity createdEntry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creating);
        ArgumentNullException.ThrowIfNull(createdEntry);
        return UnavailableTransition();
    }

    public Task<PreparedCodeGraphOwnershipTransitionResult> BeginDispositionAsync(
        PreparedCodeGraphWorkspaceOwnership ready,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ready);
        return UnavailableTransition();
    }

    public Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
        PreparedCodeGraphWorkspaceOwnership disposing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disposing);
        return UnavailableTransition();
    }

    public Task<PreparedCodeGraphOwnershipTransitionResult> QuarantineAsync(
        PreparedCodeGraphWorkspaceOwnership current,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        return UnavailableTransition();
    }

    private static PreparedCodeGraphOwnershipLoadResult UnavailableLoad() =>
        new(PreparedCodeGraphOwnershipLoadStatus.Unavailable, null);

    private static Task<PreparedCodeGraphOwnershipTransitionResult> UnavailableTransition() =>
        Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
            PreparedCodeGraphOwnershipTransitionStatus.Unavailable, null));

    private static PreparedToolDirectoryObjectIdentity? Stable(SafeFileHandle? directory)
    {
        PreparedSourceTraversal.DirectoryIdentity? observed = directory is null || directory.IsInvalid
            ? null : PreparedSourceTraversal.ObserveDirectory(directory);
        return observed is null ? null : new PreparedToolDirectoryObjectIdentity(
            observed.VolumeSerialNumber, observed.FileIndexHigh, observed.FileIndexLow);
    }

    private static bool SeparateRoots(string storageRoot, string workspaceRoot)
    {
        string storage = Path.TrimEndingDirectorySeparator(Path.GetFullPath(storageRoot));
        string workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        string storagePrefix = storage + Path.DirectorySeparatorChar;
        string workspacePrefix = workspace + Path.DirectorySeparatorChar;
        return !string.Equals(storage, workspace, StringComparison.OrdinalIgnoreCase) &&
            !storage.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase) &&
            !workspace.StartsWith(storagePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static (int Status, SafeFileHandle? Handle) OpenRelative(
        SafeFileHandle parent, string name, bool directory, bool create)
    {
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
                    out SafeFileHandle handle,
                    directory
                        ? 0x80u | 0x1u | 0x20u | 0x00100000u
                        : create
                            ? 0x1u | 0x2u | 0x80u | 0x00100000u
                            : 0x1u | 0x80u | 0x00100000u,
                    ref attributes,
                    out _,
                    IntPtr.Zero,
                    directory ? (uint)FileAttributes.Directory : 0,
                    0x00000007, // share read/write/delete
                    create ? 2u : 1u, // FILE_CREATE or FILE_OPEN
                    (directory ? 0x1u : 0x40u) | 0x00200000u | 0x20u,
                    IntPtr.Zero,
                    0);
                if (status < 0 || handle.IsInvalid)
                {
                    handle?.Dispose();
                    return (status, null);
                }

                return (status, handle);
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
}
