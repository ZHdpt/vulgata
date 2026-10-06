using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

internal sealed class WindowsPreparedManagedWorkspaceHostVerifier : IPreparedManagedWorkspaceHostVerifier
{
    private const string MetadataName = ".prepared-codegraph-ownership";
    private readonly SecurityIdentifier _approvedPrincipal;

    internal WindowsPreparedManagedWorkspaceHostVerifier(SecurityIdentifier approvedApplicationPrincipal)
    {
        _approvedPrincipal = approvedApplicationPrincipal ??
            throw new ArgumentNullException(nameof(approvedApplicationPrincipal));
    }

    public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryOpenProtected(applicationHeldSourceGrant?.PhysicalRootPath,
                sourceStoragePolicy, out ProtectedObjects? observed))
        {
            return Task.FromResult(new PreparedManagedWorkspaceHostObservation(
                PreparedManagedWorkspaceHostStatus.Unavailable, null, null));
        }

        using (observed)
        {
            return Task.FromResult(new PreparedManagedWorkspaceHostObservation(
                PreparedManagedWorkspaceHostStatus.Observed,
                observed.RootIdentity,
                OwnerIdentity(observed.RootIdentity)));
        }
    }

    public async Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (binding is null || !TryOpenProtected(binding.PhysicalRootPath,
                sourceStoragePolicy, out ProtectedObjects? objects))
        {
            return UnavailableHold();
        }

        NamedMutexOwner? mutexOwner = null;
        bool transferred = false;
        bool cleanupFailed = false;
        OperationCanceledException? cancellation = null;
        PreparedManagedWorkspaceHostHoldResult result = UnavailableHold();
        try
        {
            if (objects.RootIdentity != binding.RootObjectIdentity ||
                !string.Equals(binding.WorkspaceOwnerIdentity,
                    OwnerIdentity(objects.RootIdentity), StringComparison.Ordinal))
            {
                return result;
            }

            mutexOwner = new NamedMutexOwner(
                $@"Local\Vulgata.PreparedWorkspace.{OwnerIdentity(objects.RootIdentity)}",
                _approvedPrincipal);
            if (!await mutexOwner.AcquireAsync(cancellationToken))
            {
                return result;
            }

            if (!TryOpenProtected(binding.PhysicalRootPath, sourceStoragePolicy,
                    out ProtectedObjects? current))
            {
                return result;
            }

            using (current)
            {
                if (!objects.SameObjects(current))
                {
                    return result;
                }
            }

            transferred = true;
            result = new PreparedManagedWorkspaceHostHoldResult(
                PreparedManagedWorkspaceHostStatus.Protected,
                new HeldProtection(this, binding, sourceStoragePolicy, objects, mutexOwner));
        }
        catch (OperationCanceledException exception)
        {
            cancellation = exception;
        }
        catch
        {
            // A failed or uncertain acquisition cannot transfer a host hold.
        }
        finally
        {
            if (!transferred)
            {
                try
                {
                    if (mutexOwner is not null)
                    {
                        await mutexOwner.DisposeAsync();
                    }
                }
                catch
                {
                    cleanupFailed = true;
                }
                finally
                {
                    try
                    {
                        objects.Dispose();
                    }
                    catch
                    {
                        cleanupFailed = true;
                    }
                }
            }
        }

        if (cancellation is not null && !cleanupFailed)
        {
            throw cancellation;
        }

        return result;
    }

    private bool TryOpenProtected(string? rootPath, RetainedSourceCapturePolicy? policy,
        out ProtectedObjects? objects)
    {
        objects = null;
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(rootPath) ||
            policy is null || !Path.IsPathFullyQualified(rootPath) ||
            !Path.IsPathFullyQualified(policy.RootPath))
        {
            return false;
        }

        SafeFileHandle? root = null;
        SafeFileHandle? storage = null;
        SafeFileHandle? metadata = null;
        try
        {
            using WindowsIdentity effective = WindowsIdentity.GetCurrent();
            if (effective.User is null || !effective.User.Equals(_approvedPrincipal))
            {
                return false;
            }

            string absoluteRoot = Path.GetFullPath(rootPath);
            string absoluteStorage = Path.GetFullPath(policy.RootPath);
            if (Overlaps(absoluteRoot, absoluteStorage))
            {
                return false;
            }

            var rootOpen = PreparedSourceTraversal.OpenRoot(absoluteRoot);
            root = rootOpen.Handle;
            var storageOpen = PreparedSourceTraversal.OpenRoot(absoluteStorage);
            storage = storageOpen.Handle;
            if (rootOpen.Status != PreparedSourceTraversal.OpenStatus.Opened ||
                storageOpen.Status != PreparedSourceTraversal.OpenStatus.Opened ||
                root is null || storage is null)
            {
                return false;
            }

            var metadataOpen = PreparedSourceTraversal.OpenRelativeDirectory(storage, MetadataName);
            metadata = metadataOpen.Handle;
            if (metadataOpen.Status != PreparedSourceTraversal.OpenStatus.Opened || metadata is null)
            {
                return false;
            }

            PreparedToolDirectoryObjectIdentity? rootIdentity = Identity(root);
            PreparedToolDirectoryObjectIdentity? storageIdentity = Identity(storage);
            PreparedToolDirectoryObjectIdentity? metadataIdentity = Identity(metadata);
            if (rootIdentity is null || storageIdentity is null || metadataIdentity is null ||
                rootIdentity == storageIdentity || rootIdentity == metadataIdentity ||
                storageIdentity == metadataIdentity || !ProtectedDacl(absoluteRoot, root) ||
                !ProtectedDacl(absoluteStorage, storage) ||
                !ProtectedDacl(Path.Combine(absoluteStorage, MetadataName), metadata))
            {
                return false;
            }

            objects = new ProtectedObjects(absoluteRoot, absoluteStorage, root,
                storage, metadata, rootIdentity, storageIdentity, metadataIdentity);
            root = null;
            storage = null;
            metadata = null;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
                   UnauthorizedAccessException or NotSupportedException or PathTooLongException or
                   System.Security.SecurityException)
        {
            return false;
        }
        finally
        {
            metadata?.Dispose();
            storage?.Dispose();
            root?.Dispose();
        }
    }

    private bool ProtectedDacl(string path, SafeFileHandle held)
    {
        const uint fileObject = 1;
        const uint daclSecurityInformation = 4;
        const uint readControl = 0x00020000;
        const uint readAttributes = 0x00000080;
        const uint shareReadWriteDelete = 7;
        const uint openExisting = 3;
        const uint noFollowDirectory = 0x02200000;
        using SafeFileHandle securityHandle = CreateFileW(path,
            readControl | readAttributes, shareReadWriteDelete, IntPtr.Zero,
            openExisting, noFollowDirectory, IntPtr.Zero);
        if (securityHandle.IsInvalid || Identity(securityHandle) != Identity(held) ||
            GetSecurityInfo(securityHandle, fileObject, daclSecurityInformation,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                out IntPtr descriptor) != 0 || descriptor == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            uint length = GetSecurityDescriptorLength(descriptor);
            if (length is 0 or > 65_536)
            {
                return false;
            }

            byte[] bytes = new byte[length];
            Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            var security = new RawSecurityDescriptor(bytes, 0);
            if ((security.ControlFlags & ControlFlags.DiscretionaryAclProtected) == 0 ||
                security.DiscretionaryAcl is not { Count: > 0 } dacl)
            {
                return false;
            }

            foreach (GenericAce ace in dacl)
            {
                if (ace is not CommonAce rule || rule.IsInherited ||
                    rule.AceQualifier != AceQualifier.AccessAllowed ||
                    rule.AccessMask == 0 ||
                    !rule.SecurityIdentifier.Equals(_approvedPrincipal))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    private string OwnerIdentity(PreparedToolDirectoryObjectIdentity root)
    {
        string material = $"{_approvedPrincipal.Value}:{root.VolumeSerialNumber:X8}:" +
            $"{root.FileIndexHigh:X8}:{root.FileIndexLow:X8}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static PreparedToolDirectoryObjectIdentity? Identity(SafeFileHandle handle)
    {
        PreparedSourceTraversal.DirectoryIdentity? observed =
            PreparedSourceTraversal.ObserveDirectory(handle);
        return observed is null ? null : new PreparedToolDirectoryObjectIdentity(
            observed.VolumeSerialNumber, observed.FileIndexHigh, observed.FileIndexLow);
    }

    private static bool Overlaps(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase) ||
        first.StartsWith(second.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase) ||
        second.StartsWith(first.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static PreparedManagedWorkspaceHostHoldResult UnavailableHold() =>
        new(PreparedManagedWorkspaceHostStatus.Unavailable, null);

    private sealed class ProtectedObjects(
        string rootPath, string storagePath, SafeFileHandle root,
        SafeFileHandle storage, SafeFileHandle metadata,
        PreparedToolDirectoryObjectIdentity rootIdentity,
        PreparedToolDirectoryObjectIdentity storageIdentity,
        PreparedToolDirectoryObjectIdentity metadataIdentity) : IDisposable
    {
        internal string RootPath { get; } = rootPath;
        internal string StoragePath { get; } = storagePath;
        internal SafeFileHandle Root { get; } = root;
        internal SafeFileHandle Storage { get; } = storage;
        internal SafeFileHandle Metadata { get; } = metadata;
        internal PreparedToolDirectoryObjectIdentity RootIdentity { get; } = rootIdentity;
        internal PreparedToolDirectoryObjectIdentity StorageIdentity { get; } = storageIdentity;
        internal PreparedToolDirectoryObjectIdentity MetadataIdentity { get; } = metadataIdentity;

        internal bool SameObjects(ProtectedObjects other) =>
            RootIdentity == other.RootIdentity && StorageIdentity == other.StorageIdentity &&
            MetadataIdentity == other.MetadataIdentity;

        public void Dispose()
        {
            Metadata.Dispose();
            Storage.Dispose();
            Root.Dispose();
        }
    }

    private sealed class HeldProtection(
        WindowsPreparedManagedWorkspaceHostVerifier verifier,
        PreparedCodeGraphWorkspaceOwnership admitted,
        RetainedSourceCapturePolicy policy,
        ProtectedObjects objects, NamedMutexOwner mutexOwner) : IPreparedManagedWorkspaceHostHold
    {
        private readonly object _disposalGate = new();
        private Task? _disposal;
        private int _closing;

        public ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref _closing) != 0 || !mutexOwner.IsOwned ||
                binding is null || binding.OwnershipId != admitted.OwnershipId ||
                binding.Approval != admitted.Approval ||
                !string.Equals(binding.PhysicalRootPath, objects.RootPath,
                    StringComparison.OrdinalIgnoreCase) ||
                binding.RootObjectIdentity != objects.RootIdentity ||
                !string.Equals(binding.WorkspaceOwnerIdentity,
                    verifier.OwnerIdentity(objects.RootIdentity), StringComparison.Ordinal) ||
                !string.Equals(policy.RootPath, objects.StoragePath,
                    StringComparison.OrdinalIgnoreCase) ||
                !verifier.TryOpenProtected(objects.RootPath, policy, out ProtectedObjects? current))
            {
                return ValueTask.FromResult(PreparedManagedWorkspaceHostStatus.Unavailable);
            }

            using (current)
            {
                return ValueTask.FromResult(objects.SameObjects(current) &&
                    verifier.ProtectedDacl(objects.RootPath, objects.Root) &&
                    verifier.ProtectedDacl(objects.StoragePath, objects.Storage) &&
                    verifier.ProtectedDacl(Path.Combine(objects.StoragePath, MetadataName),
                        objects.Metadata)
                        ? PreparedManagedWorkspaceHostStatus.Protected
                        : PreparedManagedWorkspaceHostStatus.Unavailable);
            }
        }

        public ValueTask DisposeAsync()
        {
            lock (_disposalGate)
            {
                if (_disposal is null)
                {
                    Volatile.Write(ref _closing, 1);
                    _disposal = DisposeOwnedAsync();
                }

                return new ValueTask(_disposal);
            }
        }

        private async Task DisposeOwnedAsync()
        {
            try
            {
                await mutexOwner.DisposeAsync();
            }
            finally
            {
                objects.Dispose();
            }
        }
    }

    private sealed class NamedMutexOwner(string name, SecurityIdentifier principal) : IAsyncDisposable
    {
        private readonly ManualResetEventSlim _stop = new(false);
        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _ended =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _disposalGate = new();
        private Thread? _thread;
        private Task? _disposal;

        internal bool IsOwned => _started.Task.IsCompletedSuccessfully &&
            _started.Task.Result && !_ended.Task.IsCompleted;

        internal async Task<bool> AcquireAsync(CancellationToken cancellationToken)
        {
            var thread = new Thread(Run) { IsBackground = true, Name = "Vulgata workspace host hold" };
            thread.Start();
            _thread = thread;
            try
            {
                return await _started.Task.WaitAsync(cancellationToken);
            }
            catch
            {
                _stop.Set();
                throw;
            }
        }

        private void Run()
        {
            Mutex? mutex = null;
            bool ownsMutex = false;
            Exception? failure = null;
            try
            {
                var security = new MutexSecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new MutexAccessRule(principal,
                    MutexRights.FullControl, AccessControlType.Allow));
                mutex = MutexAcl.Create(initiallyOwned: true, name,
                    out bool createdNew, security);
                ownsMutex = createdNew;
                _started.TrySetResult(ownsMutex);
                if (ownsMutex)
                {
                    _stop.Wait();
                }
            }
            catch (Exception exception)
            {
                failure = exception;
                _started.TrySetException(exception);
            }
            finally
            {
                try
                {
                    if (ownsMutex)
                    {
                        mutex!.ReleaseMutex();
                    }
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }

                try
                {
                    mutex?.Dispose();
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }

                if (failure is null)
                {
                    _ended.TrySetResult();
                }
                else
                {
                    _ended.TrySetException(failure);
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            lock (_disposalGate)
            {
                _disposal ??= StopAsync();
                return new ValueTask(_disposal);
            }
        }

        private async Task StopAsync()
        {
            _stop.Set();
            try
            {
                if (_thread is not null)
                {
                    await _ended.Task;
                }
            }
            finally
            {
                _stop.Dispose();
            }
        }
    }

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint GetSecurityInfo(SafeFileHandle handle,
        uint objectType, uint securityInfo, IntPtr owner, IntPtr group,
        IntPtr dacl, IntPtr sacl, out IntPtr descriptor);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true,
        EntryPoint = "CreateFileW", ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string path,
        uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr template);
}
