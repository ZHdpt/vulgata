using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private const string LockFileName = "ledger.lock";
    private const string LedgerFormat = "vulgata-codegraph-ownership-v1";
    private const int MaximumLedgerBytes = 1_048_576;
    private static readonly byte[] EmptyLedger = Encoding.UTF8.GetBytes(
        "{\"format\":\"vulgata-codegraph-ownership-v1\",\"records\":[]}");

    private sealed record LedgerDocument(
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("records")] PreparedCodeGraphWorkspaceOwnership[] Records);

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
            bool absentDraft = expectedBinding.Phase == PreparedCodeGraphOwnershipPhase.Absent &&
                expectedBinding.Version == 0 && expectedBinding.StateEntryObjectIdentity is null;
            bool recordedPhase = (expectedBinding.Phase is
                PreparedCodeGraphOwnershipPhase.Creating or PreparedCodeGraphOwnershipPhase.Ready or
                PreparedCodeGraphOwnershipPhase.Disposing or PreparedCodeGraphOwnershipPhase.Removed) &&
                expectedBinding.Version > 0;
            if ((!absentDraft && !recordedPhase) ||
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

                    LedgerDocument? ledger = await ReadLedgerAsync(metadata!, cancellationToken);
                    if (ledger is null || Stable(root) != rootIdentity ||
                        Stable(metadata) != metadataIdentity)
                    {
                        return UnavailableLoad();
                    }

                    PreparedCodeGraphWorkspaceOwnership[] matches = ledger.Records
                        .Where(record => SameWorkspace(record, expectedBinding) ||
                            record.OwnershipId == expectedBinding.OwnershipId).ToArray();
                    if (matches.Length == 1 && SameBinding(matches[0], expectedBinding))
                    {
                        return new PreparedCodeGraphOwnershipLoadResult(
                            PreparedCodeGraphOwnershipLoadStatus.Found, matches[0]);
                    }

                    return matches.Length == 0 && absentDraft
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

    public async Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
        PreparedCodeGraphWorkspaceOwnership absent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(absent);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (absent.OwnershipId == Guid.Empty || absent.Phase != PreparedCodeGraphOwnershipPhase.Absent ||
                absent.Version != 0 || absent.StateEntryObjectIdentity is not null ||
                absent.Approval.RepositoryId == Guid.Empty ||
                string.IsNullOrWhiteSpace(absent.WorkspaceOwnerIdentity) ||
                !SeparateRoots(_sourceStoragePolicy.RootPath, absent.PhysicalRootPath))
            {
                return UnavailableTransitionResult();
            }

            (PreparedSourceTraversal.OpenStatus workspaceStatus, SafeFileHandle? workspace) =
                PreparedSourceTraversal.OpenRoot(absent.PhysicalRootPath);
            using (workspace)
            {
                if (workspaceStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                    Stable(workspace) != absent.RootObjectIdentity)
                {
                    return UnavailableTransitionResult();
                }
            }

            (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? root) =
                PreparedSourceTraversal.OpenRoot(_sourceStoragePolicy.RootPath);
            using (root)
            {
                PreparedToolDirectoryObjectIdentity? rootIdentity = Stable(root);
                if (rootStatus != PreparedSourceTraversal.OpenStatus.Opened || rootIdentity is null)
                {
                    return UnavailableTransitionResult();
                }

                (PreparedSourceTraversal.OpenStatus metadataStatus, SafeFileHandle? metadata) =
                    PreparedSourceTraversal.OpenRelativeDirectory(root!, MetadataDirectoryName);
                using (metadata)
                {
                    PreparedToolDirectoryObjectIdentity? metadataIdentity = Stable(metadata);
                    if (metadataStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                        metadataIdentity is null)
                    {
                        return UnavailableTransitionResult();
                    }

                    (int lockStatus, SafeFileHandle? exclusiveLock) = OpenRelative(
                        metadata!, LockFileName, directory: false, create: false, exclusiveLock: true);
                    using (exclusiveLock)
                    {
                        if (lockStatus < 0 || exclusiveLock is null || exclusiveLock.IsInvalid ||
                            PreparedSourceTraversal.ObserveFile(exclusiveLock) is null)
                        {
                            return UnavailableTransitionResult();
                        }

                        LedgerDocument? ledger = await ReadLedgerAsync(metadata!, cancellationToken);
                        if (ledger is null || Stable(root) != rootIdentity ||
                            Stable(metadata) != metadataIdentity)
                        {
                            return UnavailableTransitionResult();
                        }

                        if (ledger.Records.Any(record => SameWorkspace(record, absent) ||
                            record.OwnershipId == absent.OwnershipId))
                        {
                            return new PreparedCodeGraphOwnershipTransitionResult(
                                PreparedCodeGraphOwnershipTransitionStatus.Conflict, null);
                        }

                        PreparedCodeGraphWorkspaceOwnership creating = absent with
                        {
                            Version = 1,
                            Phase = PreparedCodeGraphOwnershipPhase.Creating,
                        };
                        var updated = new LedgerDocument(LedgerFormat, [.. ledger.Records, creating]);
                        if (!await ReplaceLedgerAsync(metadata!, updated, cancellationToken) ||
                            Stable(root) != rootIdentity || Stable(metadata) != metadataIdentity)
                        {
                            return UnavailableTransitionResult();
                        }

                        LedgerDocument? readback = await ReadLedgerAsync(metadata!, cancellationToken);
                        return readback is not null &&
                            readback.Records.SequenceEqual(updated.Records) &&
                            Stable(root) == rootIdentity && Stable(metadata) == metadataIdentity
                            ? new PreparedCodeGraphOwnershipTransitionResult(
                                PreparedCodeGraphOwnershipTransitionStatus.Committed, creating)
                            : UnavailableTransitionResult();
                    }
                }
            }
        }
        catch (Exception)
        {
            return UnavailableTransitionResult();
        }
    }

    public async Task<PreparedCodeGraphOwnershipTransitionResult> CommitCreatedAsync(
        PreparedCodeGraphWorkspaceOwnership creating,
        PreparedToolDirectoryObjectIdentity createdEntry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creating);
        ArgumentNullException.ThrowIfNull(createdEntry);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (creating.OwnershipId == Guid.Empty ||
                creating.Phase != PreparedCodeGraphOwnershipPhase.Creating ||
                creating.Version <= 0 || creating.StateEntryObjectIdentity is not null ||
                !SeparateRoots(_sourceStoragePolicy.RootPath, creating.PhysicalRootPath))
            {
                return UnavailableTransitionResult();
            }

            (PreparedSourceTraversal.OpenStatus workspaceStatus, SafeFileHandle? workspace) =
                PreparedSourceTraversal.OpenRoot(creating.PhysicalRootPath);
            using (workspace)
            {
                if (workspaceStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                    Stable(workspace) != creating.RootObjectIdentity)
                {
                    return UnavailableTransitionResult();
                }

                (PreparedSourceTraversal.OpenStatus entryStatus, SafeFileHandle? entry) =
                    PreparedSourceTraversal.OpenRelativeDirectory(workspace!, ".codegraph");
                using (entry)
                {
                    if (entryStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                        Stable(entry) != createdEntry)
                    {
                        return UnavailableTransitionResult();
                    }
                }

                (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? root) =
                    PreparedSourceTraversal.OpenRoot(_sourceStoragePolicy.RootPath);
                using (root)
                {
                    PreparedToolDirectoryObjectIdentity? rootIdentity = Stable(root);
                    if (rootStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                        rootIdentity is null)
                    {
                        return UnavailableTransitionResult();
                    }

                    (PreparedSourceTraversal.OpenStatus metadataStatus, SafeFileHandle? metadata) =
                        PreparedSourceTraversal.OpenRelativeDirectory(root!, MetadataDirectoryName);
                    using (metadata)
                    {
                        PreparedToolDirectoryObjectIdentity? metadataIdentity = Stable(metadata);
                        if (metadataStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                            metadataIdentity is null)
                        {
                            return UnavailableTransitionResult();
                        }

                        (int lockStatus, SafeFileHandle? exclusiveLock) = OpenRelative(
                            metadata!, LockFileName, directory: false, create: false,
                            exclusiveLock: true);
                        using (exclusiveLock)
                        {
                            if (lockStatus < 0 || exclusiveLock is null || exclusiveLock.IsInvalid ||
                                PreparedSourceTraversal.ObserveFile(exclusiveLock) is null)
                            {
                                return UnavailableTransitionResult();
                            }

                            LedgerDocument? ledger = await ReadLedgerAsync(metadata!, cancellationToken);
                            if (ledger is null || Stable(root) != rootIdentity ||
                                Stable(metadata) != metadataIdentity ||
                                Stable(workspace) != creating.RootObjectIdentity)
                            {
                                return UnavailableTransitionResult();
                            }

                            PreparedCodeGraphWorkspaceOwnership[] matches = ledger.Records
                                .Where(record => SameWorkspace(record, creating) ||
                                    record.OwnershipId == creating.OwnershipId).ToArray();
                            if (matches.Length != 1 || matches[0] != creating)
                            {
                                return new PreparedCodeGraphOwnershipTransitionResult(
                                    PreparedCodeGraphOwnershipTransitionStatus.Conflict, null);
                            }

                            PreparedCodeGraphWorkspaceOwnership ready = creating with
                            {
                                Version = checked(creating.Version + 1),
                                Phase = PreparedCodeGraphOwnershipPhase.Ready,
                                StateEntryObjectIdentity = createdEntry,
                            };
                            var updated = new LedgerDocument(LedgerFormat,
                                ledger.Records.Select(record => record == creating ? ready : record).ToArray());
                            if (!await ReplaceLedgerAsync(metadata!, updated, cancellationToken) ||
                                Stable(root) != rootIdentity || Stable(metadata) != metadataIdentity ||
                                Stable(workspace) != creating.RootObjectIdentity)
                            {
                                return UnavailableTransitionResult();
                            }

                            LedgerDocument? readback = await ReadLedgerAsync(metadata!, cancellationToken);
                            return readback is not null &&
                                readback.Records.SequenceEqual(updated.Records) &&
                                Stable(root) == rootIdentity && Stable(metadata) == metadataIdentity &&
                                Stable(workspace) == creating.RootObjectIdentity
                                ? new PreparedCodeGraphOwnershipTransitionResult(
                                    PreparedCodeGraphOwnershipTransitionStatus.Committed, ready)
                                : UnavailableTransitionResult();
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            return UnavailableTransitionResult();
        }
    }

    public async Task<PreparedCodeGraphOwnershipTransitionResult> BeginDispositionAsync(
        PreparedCodeGraphWorkspaceOwnership ready,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ready);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ready.OwnershipId == Guid.Empty ||
                ready.Phase != PreparedCodeGraphOwnershipPhase.Ready ||
                ready.Version <= 0 || ready.StateEntryObjectIdentity is null ||
                !SeparateRoots(_sourceStoragePolicy.RootPath, ready.PhysicalRootPath))
            {
                return UnavailableTransitionResult();
            }

            (PreparedSourceTraversal.OpenStatus workspaceStatus, SafeFileHandle? workspace) =
                PreparedSourceTraversal.OpenRoot(ready.PhysicalRootPath);
            using (workspace)
            {
                if (workspaceStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                    Stable(workspace) != ready.RootObjectIdentity ||
                    !MatchesCodeGraphEntry(workspace!, ready.StateEntryObjectIdentity))
                {
                    return UnavailableTransitionResult();
                }

                (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? root) =
                    PreparedSourceTraversal.OpenRoot(_sourceStoragePolicy.RootPath);
                using (root)
                {
                    PreparedToolDirectoryObjectIdentity? rootIdentity = Stable(root);
                    if (rootStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                        rootIdentity is null)
                    {
                        return UnavailableTransitionResult();
                    }

                    (PreparedSourceTraversal.OpenStatus metadataStatus, SafeFileHandle? metadata) =
                        PreparedSourceTraversal.OpenRelativeDirectory(root!, MetadataDirectoryName);
                    using (metadata)
                    {
                        PreparedToolDirectoryObjectIdentity? metadataIdentity = Stable(metadata);
                        if (metadataStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                            metadataIdentity is null)
                        {
                            return UnavailableTransitionResult();
                        }

                        (int lockStatus, SafeFileHandle? exclusiveLock) = OpenRelative(
                            metadata!, LockFileName, directory: false, create: false,
                            exclusiveLock: true);
                        using (exclusiveLock)
                        {
                            if (lockStatus < 0 || exclusiveLock is null || exclusiveLock.IsInvalid ||
                                PreparedSourceTraversal.ObserveFile(exclusiveLock) is null)
                            {
                                return UnavailableTransitionResult();
                            }

                            LedgerDocument? ledger = await ReadLedgerAsync(metadata!, cancellationToken);
                            if (ledger is null || Stable(root) != rootIdentity ||
                                Stable(metadata) != metadataIdentity ||
                                Stable(workspace) != ready.RootObjectIdentity ||
                                !MatchesCodeGraphEntry(workspace!, ready.StateEntryObjectIdentity))
                            {
                                return UnavailableTransitionResult();
                            }

                            PreparedCodeGraphWorkspaceOwnership[] matches = ledger.Records
                                .Where(record => SameWorkspace(record, ready) ||
                                    record.OwnershipId == ready.OwnershipId).ToArray();
                            if (matches.Length != 1 || matches[0] != ready)
                            {
                                return new PreparedCodeGraphOwnershipTransitionResult(
                                    PreparedCodeGraphOwnershipTransitionStatus.Conflict, null);
                            }

                            PreparedCodeGraphWorkspaceOwnership disposing = ready with
                            {
                                Version = checked(ready.Version + 1),
                                Phase = PreparedCodeGraphOwnershipPhase.Disposing,
                            };
                            var updated = new LedgerDocument(LedgerFormat,
                                ledger.Records.Select(record => record == ready ? disposing : record).ToArray());
                            if (!await ReplaceLedgerAsync(metadata!, updated, cancellationToken) ||
                                Stable(root) != rootIdentity || Stable(metadata) != metadataIdentity ||
                                Stable(workspace) != ready.RootObjectIdentity ||
                                !MatchesCodeGraphEntry(workspace!, ready.StateEntryObjectIdentity))
                            {
                                return UnavailableTransitionResult();
                            }

                            LedgerDocument? readback = await ReadLedgerAsync(metadata!, cancellationToken);
                            return readback is not null &&
                                readback.Records.SequenceEqual(updated.Records) &&
                                Stable(root) == rootIdentity && Stable(metadata) == metadataIdentity &&
                                Stable(workspace) == ready.RootObjectIdentity &&
                                MatchesCodeGraphEntry(workspace!, ready.StateEntryObjectIdentity)
                                ? new PreparedCodeGraphOwnershipTransitionResult(
                                    PreparedCodeGraphOwnershipTransitionStatus.Committed, disposing)
                                : UnavailableTransitionResult();
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            return UnavailableTransitionResult();
        }
    }

    public async Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
        PreparedCodeGraphWorkspaceOwnership disposing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disposing);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (disposing.OwnershipId == Guid.Empty ||
                disposing.Phase != PreparedCodeGraphOwnershipPhase.Disposing ||
                disposing.Version <= 0 || disposing.StateEntryObjectIdentity is null ||
                !SeparateRoots(_sourceStoragePolicy.RootPath, disposing.PhysicalRootPath))
            {
                return UnavailableTransitionResult();
            }

            (PreparedSourceTraversal.OpenStatus workspaceStatus, SafeFileHandle? workspace) =
                PreparedSourceTraversal.OpenRoot(disposing.PhysicalRootPath);
            using (workspace)
            {
                if (workspaceStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                    Stable(workspace) != disposing.RootObjectIdentity ||
                    !CodeGraphEntryAbsent(workspace!))
                {
                    return UnavailableTransitionResult();
                }

                (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? root) =
                    PreparedSourceTraversal.OpenRoot(_sourceStoragePolicy.RootPath);
                using (root)
                {
                    PreparedToolDirectoryObjectIdentity? rootIdentity = Stable(root);
                    if (rootStatus != PreparedSourceTraversal.OpenStatus.Opened || rootIdentity is null)
                    {
                        return UnavailableTransitionResult();
                    }

                    (PreparedSourceTraversal.OpenStatus metadataStatus, SafeFileHandle? metadata) =
                        PreparedSourceTraversal.OpenRelativeDirectory(root!, MetadataDirectoryName);
                    using (metadata)
                    {
                        PreparedToolDirectoryObjectIdentity? metadataIdentity = Stable(metadata);
                        if (metadataStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                            metadataIdentity is null)
                        {
                            return UnavailableTransitionResult();
                        }

                        (int lockStatus, SafeFileHandle? exclusiveLock) = OpenRelative(
                            metadata!, LockFileName, directory: false, create: false,
                            exclusiveLock: true);
                        using (exclusiveLock)
                        {
                            if (lockStatus < 0 || exclusiveLock is null || exclusiveLock.IsInvalid ||
                                PreparedSourceTraversal.ObserveFile(exclusiveLock) is null)
                            {
                                return UnavailableTransitionResult();
                            }

                            LedgerDocument? ledger = await ReadLedgerAsync(metadata!, cancellationToken);
                            if (ledger is null || Stable(root) != rootIdentity ||
                                Stable(metadata) != metadataIdentity ||
                                Stable(workspace) != disposing.RootObjectIdentity ||
                                !CodeGraphEntryAbsent(workspace!))
                            {
                                return UnavailableTransitionResult();
                            }

                            PreparedCodeGraphWorkspaceOwnership[] matches = ledger.Records
                                .Where(record => SameWorkspace(record, disposing) ||
                                    record.OwnershipId == disposing.OwnershipId).ToArray();
                            if (matches.Length != 1 || matches[0] != disposing)
                            {
                                return new PreparedCodeGraphOwnershipTransitionResult(
                                    PreparedCodeGraphOwnershipTransitionStatus.Conflict, null);
                            }

                            PreparedCodeGraphWorkspaceOwnership removed = disposing with
                            {
                                Version = checked(disposing.Version + 1),
                                Phase = PreparedCodeGraphOwnershipPhase.Removed,
                                StateEntryObjectIdentity = null,
                            };
                            var updated = new LedgerDocument(LedgerFormat,
                                ledger.Records.Select(record => record == disposing ? removed : record).ToArray());
                            if (!await ReplaceLedgerAsync(metadata!, updated, cancellationToken) ||
                                Stable(root) != rootIdentity || Stable(metadata) != metadataIdentity ||
                                Stable(workspace) != disposing.RootObjectIdentity ||
                                !CodeGraphEntryAbsent(workspace!))
                            {
                                return UnavailableTransitionResult();
                            }

                            LedgerDocument? readback = await ReadLedgerAsync(metadata!, cancellationToken);
                            return readback is not null &&
                                readback.Records.SequenceEqual(updated.Records) &&
                                Stable(root) == rootIdentity && Stable(metadata) == metadataIdentity &&
                                Stable(workspace) == disposing.RootObjectIdentity &&
                                CodeGraphEntryAbsent(workspace!)
                                ? new PreparedCodeGraphOwnershipTransitionResult(
                                    PreparedCodeGraphOwnershipTransitionStatus.Committed, removed)
                                : UnavailableTransitionResult();
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            return UnavailableTransitionResult();
        }
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

    private static PreparedCodeGraphOwnershipTransitionResult UnavailableTransitionResult() =>
        new(PreparedCodeGraphOwnershipTransitionStatus.Unavailable, null);

    private static Task<PreparedCodeGraphOwnershipTransitionResult> UnavailableTransition() =>
        Task.FromResult(UnavailableTransitionResult());

    private static async Task<LedgerDocument?> ReadLedgerAsync(
        SafeFileHandle metadata, CancellationToken cancellationToken)
    {
        (PreparedSourceTraversal.OpenStatus listingStatus,
            IReadOnlyList<PreparedSourceTraversal.ListedEntry> entries, bool complete) =
            PreparedSourceTraversal.ListImmediate(metadata, maximumEntries: 4);
        if (listingStatus != PreparedSourceTraversal.OpenStatus.Opened || !complete ||
            entries.Any(entry => entry.Name is not (LedgerFileName or LockFileName)))
        {
            return null;
        }

        (int status, SafeFileHandle? file) = OpenRelative(
            metadata, LedgerFileName, directory: false, create: false);
        if (status < 0 || file is null || file.IsInvalid ||
            PreparedSourceTraversal.ObserveFile(file) is null)
        {
            file?.Dispose();
            return null;
        }

        await using var stream = new FileStream(
            file, FileAccess.Read, bufferSize: 4096, isAsync: false);
        if (stream.Length is <= 0 or > MaximumLedgerBytes)
        {
            return null;
        }

        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        LedgerDocument? ledger = JsonSerializer.Deserialize<LedgerDocument>(bytes);
        if (ledger?.Format != LedgerFormat || ledger.Records is null ||
            !JsonSerializer.SerializeToUtf8Bytes(ledger).AsSpan().SequenceEqual(bytes) ||
            ledger.Records.Any(record => record is null ||
                record.OwnershipId == Guid.Empty || record.Version <= 0 ||
                record.Phase == PreparedCodeGraphOwnershipPhase.Absent ||
                record.Approval is null || record.RootObjectIdentity is null ||
                !Path.IsPathFullyQualified(record.PhysicalRootPath) ||
                string.IsNullOrWhiteSpace(record.WorkspaceOwnerIdentity) ||
                record.Phase == PreparedCodeGraphOwnershipPhase.Creating &&
                    record.StateEntryObjectIdentity is not null ||
                record.Phase == PreparedCodeGraphOwnershipPhase.Ready &&
                    record.StateEntryObjectIdentity is null ||
                record.Phase == PreparedCodeGraphOwnershipPhase.Disposing &&
                    record.StateEntryObjectIdentity is null ||
                record.Phase == PreparedCodeGraphOwnershipPhase.Removed &&
                    record.StateEntryObjectIdentity is not null))
        {
            return null;
        }

        for (int i = 0; i < ledger.Records.Length; i++)
        {
            for (int j = i + 1; j < ledger.Records.Length; j++)
            {
                if (ledger.Records[i].OwnershipId == ledger.Records[j].OwnershipId ||
                    SameWorkspace(ledger.Records[i], ledger.Records[j]))
                {
                    return null;
                }
            }
        }

        return ledger;
    }

    private static async Task<bool> ReplaceLedgerAsync(
        SafeFileHandle metadata, LedgerDocument updated, CancellationToken cancellationToken)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(updated);
        if (bytes.Length > MaximumLedgerBytes)
        {
            return false;
        }

        string tempName = $"ledger-{Guid.NewGuid():N}.tmp";
        (int status, SafeFileHandle? file) = OpenRelative(
            metadata, tempName, directory: false, create: true, renameSource: true);
        if (status < 0 || file is null || file.IsInvalid ||
            PreparedSourceTraversal.ObserveFile(file) is null)
        {
            file?.Dispose();
            return false;
        }

        await using var stream = new FileStream(
            file, FileAccess.ReadWrite, bufferSize: 4096, isAsync: false);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
        cancellationToken.ThrowIfCancellationRequested();
        return RenameOverLedger(stream.SafeFileHandle, metadata);
    }

    private static bool RenameOverLedger(SafeFileHandle source, SafeFileHandle metadata)
    {
        byte[] name = Encoding.Unicode.GetBytes(LedgerFileName);
        int rootOffset = IntPtr.Size == 8 ? 8 : 4;
        int lengthOffset = rootOffset + IntPtr.Size;
        int nameOffset = lengthOffset + sizeof(int);
        int headerSize = IntPtr.Size == 8 ? 24 : 16;
        int bufferSize = checked(headerSize + name.Length);
        IntPtr information = Marshal.AllocHGlobal(bufferSize);
        bool referenceAdded = false;
        try
        {
            Marshal.Copy(new byte[bufferSize], 0, information, bufferSize);
            Marshal.WriteByte(information, 0, 1); // ReplaceIfExists
            metadata.DangerousAddRef(ref referenceAdded);
            Marshal.WriteIntPtr(information, rootOffset, metadata.DangerousGetHandle());
            Marshal.WriteInt32(information, lengthOffset, name.Length);
            Marshal.Copy(name, 0, IntPtr.Add(information, nameOffset), name.Length);
            int status = NtSetInformationFile(
                source, out _, information, (uint)bufferSize, 10); // FileRenameInformation
            return status == 0;
        }
        finally
        {
            if (referenceAdded)
            {
                metadata.DangerousRelease();
            }

            Marshal.FreeHGlobal(information);
        }
    }

    private static bool SameBinding(
        PreparedCodeGraphWorkspaceOwnership stored,
        PreparedCodeGraphWorkspaceOwnership expected) =>
        stored.OwnershipId == expected.OwnershipId &&
        stored.Approval == expected.Approval &&
        string.Equals(stored.PhysicalRootPath, expected.PhysicalRootPath,
            StringComparison.OrdinalIgnoreCase) &&
        stored.RootObjectIdentity == expected.RootObjectIdentity &&
        stored.WorkspaceOwnerIdentity == expected.WorkspaceOwnerIdentity;

    private static bool SameWorkspace(
        PreparedCodeGraphWorkspaceOwnership left,
        PreparedCodeGraphWorkspaceOwnership right) =>
        string.Equals(left.PhysicalRootPath, right.PhysicalRootPath,
            StringComparison.OrdinalIgnoreCase) ||
        left.RootObjectIdentity == right.RootObjectIdentity ||
        left.WorkspaceOwnerIdentity == right.WorkspaceOwnerIdentity;

    private static PreparedToolDirectoryObjectIdentity? Stable(SafeFileHandle? directory)
    {
        PreparedSourceTraversal.DirectoryIdentity? observed = directory is null || directory.IsInvalid
            ? null : PreparedSourceTraversal.ObserveDirectory(directory);
        return observed is null ? null : new PreparedToolDirectoryObjectIdentity(
            observed.VolumeSerialNumber, observed.FileIndexHigh, observed.FileIndexLow);
    }

    private static bool MatchesCodeGraphEntry(
        SafeFileHandle workspace,
        PreparedToolDirectoryObjectIdentity expected)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? entry) =
            PreparedSourceTraversal.OpenRelativeDirectory(workspace, ".codegraph");
        using (entry)
        {
            return status == PreparedSourceTraversal.OpenStatus.Opened &&
                Stable(entry) == expected;
        }
    }

    private static bool CodeGraphEntryAbsent(SafeFileHandle workspace)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? entry) =
            PreparedSourceTraversal.OpenRelativeDirectory(workspace, ".codegraph");
        entry?.Dispose();
        return status == PreparedSourceTraversal.OpenStatus.Missing;
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
        SafeFileHandle parent, string name, bool directory, bool create,
        bool exclusiveLock = false, bool renameSource = false)
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
                        : create || exclusiveLock
                            ? 0x1u | 0x2u | 0x80u | 0x00100000u |
                                (renameSource ? 0x00010000u : 0u)
                            : 0x1u | 0x80u | 0x00100000u,
                    ref attributes,
                    out _,
                    IntPtr.Zero,
                    directory ? (uint)FileAttributes.Directory : 0,
                    exclusiveLock ? 0u : 0x00000007u, // exclusive lock or share read/write/delete
                    exclusiveLock ? 3u : create ? 2u : 1u, // FILE_OPEN_IF/CREATE/OPEN
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

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtSetInformationFile(
        SafeFileHandle file,
        out IoStatusBlock ioStatusBlock,
        IntPtr information,
        uint bufferSize,
        int informationClass);

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
